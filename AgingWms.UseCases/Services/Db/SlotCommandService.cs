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
        // 【核心修复】引入电芯仓储，专门用来清理"钉子户"数据
        private readonly IRepository<BatteryCell> _cellRepository;

        public SlotCommandService(
            IRepository<WarehouseSlot> repository,
            IRepository<BatteryCell> cellRepository) // <--- 注入它
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
                    await AddAsync(slot);
                }

                // A. 清理旧关系 (内存)
                slot.Clear();
                slot.LoadTray(trayCode);

                // B. 【防御性修复】清理数据库中的同名"孤儿"电芯
                // 防止上次移除时变成了孤儿，导致这次插入报主键冲突
                if (cells != null && cells.Any())
                {
                    foreach (var c in cells)
                    {
                        // 1. 按照即将生成的 ID 去查一下
                        // (注意：你的 DTO Barcode 其实就是 ID)
                        var existingCell = await _cellRepository.GetByIdAsync(c.Barcode);

                        // 2. 如果数据库里竟然有这个 ID (说明上次没删干净)，强制删除
                        if (existingCell != null)
                        {
                            await _cellRepository.DeleteAsync(existingCell);
                        }

                        // 3. 安全添加新电芯
                        var cell = new BatteryCell(c.Barcode, c.ChannelIndex) { IsNg = c.IsNg };
                        slot.AddCell(cell);
                    }
                }

                slot.UpdateStatus(SlotStatus.Occupied);

                await _repository.UpdateAsync(slot);
                await _repository.SaveChangesAsync();

                return new OperationResult { IsSuccess = true, Message = "入库成功" };
            }
            catch (Exception ex)
            {
                // 如果是主键冲突，通常包含 "Violation of PRIMARY KEY"
                var msg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return new OperationResult { IsSuccess = false, Message = $"入库失败: {msg}" };
            }
        }

        // 2. 移库
        public async Task<OperationResult> MoveSlotAsync(string sourceSlotId, string targetSlotId)
        {
            try
            {
                // A. 原库位出库
                var oldSlot = await GetAsync(sourceSlotId);
                string trayCode = "UNKNOWN";

                if (oldSlot != null)
                {
                    trayCode = oldSlot.TrayBarcode ?? "UNKNOWN";

                    // 【显式清理】不仅 Clear List，还要把里面的电芯删掉
                    if (oldSlot.Cells != null && oldSlot.Cells.Any())
                    {
                        var cellsToDelete = oldSlot.Cells.ToList();
                        oldSlot.Clear(); // 先断开
                        // 再物理删除
                        foreach (var cell in cellsToDelete)
                        {
                            await _cellRepository.DeleteAsync(cell);
                        }
                    }
                    else
                    {
                        oldSlot.Clear();
                    }

                    oldSlot.UpdateStatus(SlotStatus.Empty);
                    await _repository.UpdateAsync(oldSlot);
                    await _repository.SaveChangesAsync();
                }
                else
                {
                    return new OperationResult { IsSuccess = false, Message = $"源库位 {sourceSlotId} 不存在" };
                }

                // B. 新库位入库
                var newSlot = await GetAsync(targetSlotId);
                if (newSlot == null)
                {
                    newSlot = new WarehouseSlot(targetSlotId, targetSlotId);
                    await AddAsync(newSlot);
                }

                newSlot.LoadTray(trayCode);
                newSlot.UpdateStatus(SlotStatus.Occupied);

                await _repository.UpdateAsync(newSlot);
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
                    // 【显式清理】
                    if (slot.Cells != null && slot.Cells.Any())
                    {
                        var cellsToDelete = slot.Cells.ToList();
                        slot.Clear();
                        foreach (var cell in cellsToDelete)
                        {
                            await _cellRepository.DeleteAsync(cell);
                        }
                    }
                    else
                    {
                        slot.Clear();
                    }

                    slot.UpdateStatus(SlotStatus.Empty);

                    await _repository.UpdateAsync(slot);
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