using SharedKernel.Dto;
using System;
using System.Collections.Generic;

namespace SharedKernel.Contracts
{
    // 1. 保存/入库消息
    public class SaveSlotData
    {
        public string SlotId { get; set; }
        public string TrayBarcode { get; set; }
        public DateTime OperationTime { get; set; }
        public List<CellDto> Cells { get; set; } = new List<CellDto>();
    }

    // 2. 移库消息
    public class RelocateSlot
    {
        public string SourceSlotId { get; set; }
        public string TargetSlotId { get; set; }
    }

    // 3. 删除消息
    public class ClearSlot
    {
        public string SlotId { get; set; }
    }

    // ==========================================
    // 1. 查询请求与响应
    // ==========================================

    public class GetSlotQuery
    {
        public string SlotId { get; set; }
    }
}