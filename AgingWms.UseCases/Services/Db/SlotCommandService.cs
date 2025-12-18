using AgingWms.Core.Domain;
using SharedKernel.Repositoy;
using SharedKernel.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AgingWms.UseCases.Services.DB
{
    public class SlotCommandService : ResourceControlService<WarehouseSlot>
    {
        // 引入电芯仓储，用于级联删除
        private readonly IRepository<BatteryCell> _cellRepository;

        public SlotCommandService(
            IRepository<WarehouseSlot> repository,
            IRepository<BatteryCell> cellRepository)
            : base(repository)
        {
            _cellRepository = cellRepository;
        }

        // 1. 入库
        public async Task<OperationResult> WriteSlotAsync(string slotId, string trayCode, List<CellDto> cells)
        {
            try
            {
                var slot = await GetAsync(slotId);
                if (slot == null)
                {
                    slot = new WarehouseSlot(slotId, slotId);
                    await _repository.AddAsync(slot); // 内存操作
                }

                // A. 清理内存关系
                slot.Clear();
                slot.LoadTray(trayCode);

                // B. 清理数据库中的"孤儿"电芯 (防止主键冲突)
                if (cells != null && cells.Any())
                {
                    foreach (var c in cells)
                    {
                        var existingCell = await _cellRepository.GetByIdAsync(c.Barcode);
                        if (existingCell != null)
                        {
                            // 标记删除 (内存操作)
                            await _cellRepository.DeleteAsync(existingCell);
                        }

                        var cell = new BatteryCell(c.Barcode, c.ChannelIndex) { IsNg = c.IsNg };
                        slot.AddCell(cell);
                    }
                }

                slot.UpdateStatus(SlotStatus.Occupied);

                // 标记修改 (内存操作)
                await _repository.UpdateAsync(slot);

                // 【唯一提交点】所有操作一起生效
                await _repository.SaveChangesAsync();

                return new OperationResult { IsSuccess = true, Message = "入库成功" };
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return new OperationResult { IsSuccess = false, Message = $"入库失败: {msg}" };
            }
        }

        // 2. 移库
        public async Task<OperationResult> MoveSlotAsync(string sourceSlotId, string targetSlotId)
        {
            try
            {
                // --- 第一步：处理源库位 (只在内存操作) ---
                var oldSlot = await GetAsync(sourceSlotId);
                string trayCode = "UNKNOWN";

                if (oldSlot != null)
                {
                    trayCode = oldSlot.TrayBarcode ?? "UNKNOWN";

                    // 级联删除电芯
                    if (oldSlot.Cells != null && oldSlot.Cells.Any())
                    {
                        var cellsToDelete = oldSlot.Cells.ToList();
                        oldSlot.Clear(); // 断开关系
                        foreach (var cell in cellsToDelete)
                        {
                            await _cellRepository.DeleteAsync(cell); // 标记删除
                        }
                    }
                    else
                    {
                        oldSlot.Clear();
                    }

                    oldSlot.UpdateStatus(SlotStatus.Empty);
                    await _repository.UpdateAsync(oldSlot); // 标记更新
                }
                else
                {
                    return new OperationResult { IsSuccess = false, Message = $"源库位 {sourceSlotId} 不存在" };
                }

                // --- 第二步：处理目标库位 (只在内存操作) ---
                var newSlot = await GetAsync(targetSlotId);
                if (newSlot == null)
                {
                    newSlot = new WarehouseSlot(targetSlotId, targetSlotId);
                    await _repository.AddAsync(newSlot); // 标记新增
                }

                newSlot.LoadTray(trayCode);
                newSlot.UpdateStatus(SlotStatus.Occupied);

                await _repository.UpdateAsync(newSlot); // 标记更新

                // --- 第三步：【关键修复】统一提交 ---
                // 只有这里成功了，移库才算完成；否则源库位和目标库位都不会变
                await _repository.SaveChangesAsync();

                return new OperationResult { IsSuccess = true, Message = "移库成功" };
            }
            catch (Exception ex)
            {
                return new OperationResult { IsSuccess = false, Message = $"移库失败: {ex.Message}" };
            }
        }

        // 3. 清库
        public async Task<OperationResult> RemoveSlotAsync(string slotId)
        {
            try
            {
                var slot = await GetAsync(slotId);
                if (slot != null)
                {
                    if (slot.Cells != null && slot.Cells.Any())
                    {
                        var cellsToDelete = slot.Cells.ToList();
                        slot.Clear();
                        foreach (var cell in cellsToDelete)
                        {
                            await _cellRepository.DeleteAsync(cell); // 标记删除
                        }
                    }
                    else
                    {
                        slot.Clear();
                    }

                    slot.UpdateStatus(SlotStatus.Empty);

                    await _repository.UpdateAsync(slot); // 标记更新

                    // 【唯一提交点】
                    await _repository.SaveChangesAsync();

                    return new OperationResult { IsSuccess = true, Message = "清库成功" };
                }
                return new OperationResult { IsSuccess = false, Message = "库位不存在" };
            }
            catch (Exception ex)
            {
                return new OperationResult { IsSuccess = false, Message = $"清库失败: {ex.Message}" };
            }
        }

        // 兼容方法
        public async Task WriteSlotAsync(string slotId, string trayCode, string slotName = "")
        {
            await WriteSlotAsync(slotId, trayCode, new List<CellDto>());
        }
    }
}