using System;
using System.Collections.Generic;
using System.Text;

namespace AgingWms.Core.Domain
{
    /// <summary>
    /// 2. 电芯 (Battery Cell)
    /// </summary>
    public class BatteryCell
    {
        public string Barcode { get; set; }     // 电芯条码
        public int ChannelIndex { get; set; }   // 通道号 (1-N)
        public bool IsNg { get; set; }          // 是否NG

        // 【关键】N个工部数据 - 必须初始化 List
        public List<ProcessStepData> ProcessSteps { get; set; } = new List<ProcessStepData>();
    }
}