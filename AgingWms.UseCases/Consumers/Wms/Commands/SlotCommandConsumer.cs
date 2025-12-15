using AgingWms.Core.Domain;
using AutoMapper;
using MassTransit;
using SharedKernel.Contracts;
using SharedKernel.Dto;
using SharedKernel.Repositoy;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AgingWms.UseCases.Consumers.Wms.Commands
{
    public class SlotCommandConsumer :
        IConsumer<SaveSlotData>,
        IConsumer<RelocateSlot>,
        IConsumer<ClearSlot>
    {
        private readonly IRepository<WarehouseSlot> _repository;
        private readonly IMapper _mapper; // 注入 AutoMapper

        public SlotCommandConsumer(IRepository<WarehouseSlot> repository, IMapper mapper)
        {
            _repository = repository;
            _mapper = mapper;
        }

        // 1. 写操作：保存/更新库位 (Upsert)
        public async Task Consume(ConsumeContext<SaveSlotData> context)
        {
            try
            {
                var msg = context.Message;
                var slot = await _repository.GetByIdAsync(msg.SlotId);

                if (slot == null)
                {
                    // 新增模式
                    slot = new WarehouseSlot
                    {
                        SlotId = msg.SlotId,
                        SlotName = msg.SlotId,
                        // 确保集合初始化
                        Cells = new List<BatteryCell>()
                    };
                    _repository.Add(slot);
                }
                else
                {
                    // 更新模式
                    _repository.Update(slot);
                }

                // --- 业务逻辑 ---
                slot.TrayBarcode = msg.TrayBarcode;
                slot.LastUpdatedTime = msg.OperationTime;
                slot.Status = SlotStatus.Occupied;

                // --- AutoMapper 核心转换 ---
                // 将 DTO 列表 (List<CellDto>) 转换为 实体列表 (List<BatteryCell>)
                // 具体的字段映射规则已经在 Profile 中配置好了
                slot.Cells = _mapper.Map<List<BatteryCell>>(msg.Cells);

                // --- 提交事务 ---
                await _repository.SaveChangesAsync();

                // --- 响应结果 (Request/Response) ---
                await context.RespondAsync(new OperationResult
                {
                    IsSuccess = true,
                    Message = "写入成功"
                });

                // 可选：发布事件 (类似于你代码里的 bus.PublishAsync(new FeedCreatedEvent...))
                // await context.Publish(new SlotUpdatedEvent { SlotId = slot.SlotId });
            }
            catch (Exception ex)
            {
                // 异常处理并返回失败消息
                await context.RespondAsync(new OperationResult
                {
                    IsSuccess = false,
                    Message = $"写入失败: {ex.Message}"
                });
            }
        }

        // 2. 库位迁移 (修复 NULL 报错)
        public async Task Consume(ConsumeContext<RelocateSlot> context)
        {
            try
            {
                var msg = context.Message;

                // 1. 检查源
                var source = await _repository.GetByIdAsync(msg.SourceSlotId);
                if (source == null || source.Status == SlotStatus.Empty)
                {
                    await context.RespondAsync(new OperationResult { IsSuccess = false, Message = "源库位为空" });
                    return;
                }

                // 2. 准备目标
                var target = await _repository.GetByIdAsync(msg.TargetSlotId);
                if (target == null)
                {
                    target = new WarehouseSlot
                    {
                        SlotId = msg.TargetSlotId,
                        SlotName = msg.TargetSlotId,
                        Status = SlotStatus.Empty,
                        // 【关键修复】新建时给空字符串，不要给 null
                        TrayBarcode = string.Empty,
                        LastUpdatedTime = DateTime.Now,
                        Cells = new List<BatteryCell>()
                    };
                    _repository.Add(target);
                }
                else
                {
                    if (target.Status == SlotStatus.Occupied)
                    {
                        await context.RespondAsync(new OperationResult { IsSuccess = false, Message = "目标库位已有货物" });
                        return;
                    }
                    _repository.Update(target);
                }

                // 3. 搬运
                target.TrayBarcode = source.TrayBarcode;
                target.Status = SlotStatus.Occupied;
                target.LastUpdatedTime = DateTime.Now;
                target.Cells = new List<BatteryCell>(source.Cells);

                // 4. 清空源
                // 【关键修复】这里必须给空字符串，不能给 null，因为数据库不允许
                source.TrayBarcode = string.Empty;

                source.Status = SlotStatus.Empty;
                source.LastUpdatedTime = DateTime.Now;
                source.Cells = new List<BatteryCell>();
                _repository.Update(source);

                await _repository.SaveChangesAsync();

                await context.RespondAsync(new OperationResult
                {
                    IsSuccess = true,
                    Message = $"迁移成功: {msg.SourceSlotId} -> {msg.TargetSlotId}"
                });
            }
            catch (Exception ex)
            {
                var error = ex.InnerException?.Message ?? ex.Message;
                await context.RespondAsync(new OperationResult { IsSuccess = false, Message = $"迁移失败: {error}" });
            }
        }

        // 3. 库位移除 (修复 NULL 报错)
        public async Task Consume(ConsumeContext<ClearSlot> context)
        {
            try
            {
                var slot = await _repository.GetByIdAsync(context.Message.SlotId);
                if (slot != null)
                {
                    // 方式 A: 物理删除 (直接从表里删掉行)
                    // _repository.Delete(slot);

                    // 方式 B: 逻辑清空 (保留库位，只清空数据) - 推荐
                    slot.Status = SlotStatus.Empty;

                    // 【关键修复】给空字符串
                    slot.TrayBarcode = string.Empty;

                    slot.Cells = new List<BatteryCell>();
                    slot.LastUpdatedTime = DateTime.Now;

                    _repository.Update(slot);

                    await _repository.SaveChangesAsync();
                }

                await context.RespondAsync(new OperationResult { IsSuccess = true, Message = "移除成功" });
            }
            catch (Exception ex)
            {
                var error = ex.InnerException?.Message ?? ex.Message;
                await context.RespondAsync(new OperationResult { IsSuccess = false, Message = $"移除失败: {error}" });
            }
        }
    }
}