using System.Collections.Generic;

namespace AgingWms.Core.Domain
{
    public class WarehouseSlot : ProcessingNode
    {
        public string? TrayBarcode { get; private set; }
        public string SlotName { get; private set; }

        // 【修复】这里使用你定义的 BatteryCell
        public virtual ICollection<BatteryCell> Cells { get; private set; } = new List<BatteryCell>();

        private WarehouseSlot()
        {
        }

        public WarehouseSlot(string slotId, string slotName) : base(slotId)
        {
            SlotName = slotName;
        }

        public void LoadTray(string trayCode)
        {
            TrayBarcode = trayCode;
            UpdateStatus(SlotStatus.Occupied);
        }

        // 添加电芯到库位
        public void AddCell(BatteryCell cell)
        {
            Cells.Add(cell);
        }

        public void Clear()
        {
            TrayBarcode = null;
            Cells.Clear();
            UpdateStatus(SlotStatus.Empty);
        }
    }
}