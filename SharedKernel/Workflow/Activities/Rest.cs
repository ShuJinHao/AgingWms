using System;
using System.Collections.Generic;
using System.Text;

namespace SharedKernel.Workflow.Activities
{
    // 1. 定义入参 (UI配置传过来的)
    public class RestArguments
    {
        public string SlotId { get; set; }     // 操作哪个库位
        public double DurationMinutes { get; set; } // 静置时长(分钟)
    }

    // 2. 定义出参 (用于记录执行结果，万一要回滚，知道回滚谁)
    public class RestLog
    {
        public string SlotId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }
}