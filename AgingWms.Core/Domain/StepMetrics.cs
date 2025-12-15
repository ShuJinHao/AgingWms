using System;
using System.Collections.Generic;
using System.Text;

namespace AgingWms.Core.Domain
{
    /// <summary>
    /// 4. 详细指标 (Metrics)
    /// 代表该工步结束时的统计数据或关键指标
    /// </summary>
    public class StepMetrics
    {
        public double StartVoltage { get; set; }
        public double EndVoltage { get; set; }
        public double AvgTemperature { get; set; }
        public double MaxTemperature { get; set; }
        public double AvgCurrent { get; set; }

        // 如果你需要记录容量，可以加在这里
        public double Capacity { get; set; }
    }
}