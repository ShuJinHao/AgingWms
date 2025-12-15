using System;
using System.Collections.Generic;
using SharedKernel.Contracts; // 确保引用 ProcessStepData 所在的命名空间

namespace AgingWms.Core.Domain
{
    /// <summary>
    /// 2. 电芯 (Battery Cell) - 升级为受控处理节点
    /// </summary>
    public class BatteryCell : ProcessingNode
    {
        // 1. Barcode 直接复用基类的 Id，不需要重复定义
        // 这样外界既可以用 Id 也可以用 Barcode，指向同一个值
        public string Barcode => Id;

        public int ChannelIndex { get; set; }   // 通道号 (1-N)
        public bool IsNg { get; set; }          // 是否NG

        // 导航属性：所属的库位 (外键)
        // 这样我们知道这个电芯在哪个库位里
        public string? WarehouseSlotId { get; private set; }

        // 【关键】N个工部数据
        // 注意：ProcessStepData 需要是一个类或结构体，确保你定义了它
        public List<ProcessStepData> ProcessSteps { get; set; } = new List<ProcessStepData>();

        // EF Core 需要无参构造
        private BatteryCell() { }

        // 构造函数：强制要求传入条码
        public BatteryCell(string barcode, int channelIndex) : base(barcode)
        {
            ChannelIndex = channelIndex;
            IsNg = false;
        }

        // 业务方法：记录工部数据
        public void AddProcessStep(ProcessStepData data)
        {
            ProcessSteps.Add(data);
            // 每次修改数据，都更新基类的时间戳，方便监控
            base.LastUpdatedTime = DateTime.Now;
        }
    }
}