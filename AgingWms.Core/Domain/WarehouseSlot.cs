using SharedKernel.Domain; // 确保引用基类命名空间
using System;
using System.Collections.Generic;

namespace AgingWms.Core.Domain
{
    public class WarehouseSlot : ProcessingNode
    {
        public string? TrayBarcode { get; private set; }
        public string SlotName { get; private set; }

        // 【补全1】Consumer 需要读取 Status 来判断是否 Running
        public SlotStatus Status { get; private set; }

        // 【补全2】UI 需要显示的当前工步
        public string? CurrentStep { get; private set; }

        // 【补全3】恢复服务需要的时间戳
        public DateTime LastUpdateTime { get; private set; }

        // 【保留】你原来的集合定义
        public virtual ICollection<BatteryCell> Cells { get; private set; } = new List<BatteryCell>();

        private WarehouseSlot()
        {
        }

        // 【保留】你原来的构造函数写法
        public WarehouseSlot(string slotId, string slotName) : base(slotId)
        {
            SlotName = slotName;
            Status = SlotStatus.Empty;     // 初始化状态
            LastUpdateTime = DateTime.Now; // 初始化时间
        }

        public void LoadTray(string trayCode)
        {
            TrayBarcode = trayCode;
            UpdateStatus(SlotStatus.Occupied);
        }

        public void AddCell(BatteryCell cell)
        {
            // 【保留】你原来的写法，EF Core 会自动处理外键
            Cells.Add(cell);
        }

        public void Clear()
        {
            TrayBarcode = null;
            Cells.Clear();
            CurrentStep = null; // 清库时重置工步
            UpdateStatus(SlotStatus.Empty);
        }

        // 【补全4】你的代码里调用了这个方法，必须实现它
        public void UpdateStatus(SlotStatus status)
        {
            Status = status;
            LastUpdateTime = DateTime.Now;
        }

        // 【补全5】Consumer 更新工步名称专用
        public void UpdateCurrentStep(string stepName)
        {
            CurrentStep = stepName;
            LastUpdateTime = DateTime.Now;
        }
    }
}