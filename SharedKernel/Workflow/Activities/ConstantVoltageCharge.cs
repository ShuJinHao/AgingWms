using System;
using System.Collections.Generic;
using System.Text;

namespace SharedKernel.Workflow.Activities
{
    // ==========================================
    // 1. 入参：恒压充参数
    // ==========================================
    public class CvChargeArguments
    {
        public string SlotId { get; set; }
        public double TargetVoltage { get; set; }   // 恒定电压 (V)
        public double CutoffCurrent { get; set; }   // 截止电流 (A) - 当电流小于此值时停止
        public double MaxDurationMinutes { get; set; }
    }

    // ==========================================
    // 2. 出参
    // ==========================================
    public class CvChargeLog
    {
        public string SlotId { get; set; }
        public double TotalCapacityAh { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}