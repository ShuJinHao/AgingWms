using System.Collections.Generic;
using SharedKernel.Dto; // 【关键】引用 DTO 命名空间，复用那里的 CellDto

namespace SharedKernel.Contracts
{
    // 1. 基础资源指令接口
    public interface IResourceCommand
    {
        string SlotId { get; }
    }

    // 2. 保存/入库指令
    public interface SaveSlotData : IResourceCommand
    {
        string SlotId { get; }
        string SlotName { get; }
        string TrayCode { get; }
        string DataJson { get; }

        // 这里直接使用 SharedKernel.Dto.CellDto
        List<CellDto> Cells { get; }
    }

    // 3. 移库指令
    public interface RelocateSlot : IResourceCommand
    {
        string SlotId { get; }
        string TargetSlotId { get; }
    }

    // 4. 清库指令 (保留之前的)
    public interface ClearSlot : IResourceCommand
    {
        string SlotId { get; }
    }

    // ==========================================
    // 【关键修复】补充漏掉的查询契约
    // ==========================================
    public interface GetSlotQuery
    {
        string SlotId { get; }
    }
}