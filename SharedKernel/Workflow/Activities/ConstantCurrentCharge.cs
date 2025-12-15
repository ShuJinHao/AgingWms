using System;
using System.Collections.Generic;
using System.Text;

namespace SharedKernel.Workflow.Activities
{
    // ==========================================
    // 1. 入参：恒流充参数
    // ==========================================
    public class CcChargeArguments
    {
        public string SlotId { get; set; }
        public double TargetCurrent { get; set; }   // 设定电流 (A)
        public double CutoffVoltage { get; set; }   // 截止电压 (V)
        public double MaxDurationMinutes { get; set; } // 安全保护时间 (超过这个时间还没充满就报警)
    }

    // ==========================================
    // 2. 出参/日志：记录充进去多少电
    // ==========================================
    public class CcChargeLog
    {
        public string SlotId { get; set; }
        public double TotalCapacityAh { get; set; } // 累计充入容量
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string EndReason { get; set; } // 结束原因 (CutoffVoltage, Timeout, etc.)
    }
}