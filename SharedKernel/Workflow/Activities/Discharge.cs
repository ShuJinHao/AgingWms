using System;
using System.Collections.Generic;
using System.Text;

namespace SharedKernel.Workflow.Activities
{
    // ==========================================
    // 1. 入参：放电参数
    // ==========================================
    public class DischargeArguments
    {
        public string SlotId { get; set; }
        public double TargetCurrent { get; set; }   // 放电电流 (A)
        public double CutoffVoltage { get; set; }   // 截止电压 (V) - 低于此值停止
        public double MaxDurationMinutes { get; set; }
    }

    // ==========================================
    // 2. 出参
    // ==========================================
    public class DischargeLog
    {
        public string SlotId { get; set; }
        public double TotalDischargedAh { get; set; } // 放出的容量
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}