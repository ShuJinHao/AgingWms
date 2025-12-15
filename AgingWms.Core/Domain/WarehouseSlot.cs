using SharedKernel.Domain;
using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;

namespace AgingWms.Core.Domain
{
    /// <summary>
    /// 1. 库位 (Warehouse Slot) - 聚合根
    /// </summary>
    public class WarehouseSlot : IEntity, IAggregateRoot
    {
        public WarehouseSlot()
        {
            // 构造函数初始化，防止空指针
            Cells = new List<BatteryCell>();
            Status = SlotStatus.Empty;
        }

        public string SlotId { get; set; }  // 主键 (如: "1-1-1")

        // 适配 SharedKernel 的 IEntity 接口
        public object Id => SlotId;

        public string SlotName { get; set; }
        public SlotStatus Status { get; set; }
        public string TrayBarcode { get; set; }
        public DateTime LastUpdatedTime { get; set; }

        // 【关键】N个电芯 - 实际上是存 JSON
        public List<BatteryCell> Cells { get; set; }

        public bool IsEmpty() => Cells == null || !Cells.Any();
    }
}