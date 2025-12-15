using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharedKernel.Workflow.Contracts
{
    // 这个类跟之前的 AgingJobRequest 结构一样，但它是 MassTransit 的消息契约
    public class StartAgingJob
    {
        public string SlotId { get; set; }
        public string BatchId { get; set; }

        // 工步配置列表
        public List<JobStepConfig> Steps { get; set; } = new List<JobStepConfig>();
    }

    public class JobStepConfig
    {
        public string StepType { get; set; } // "Rest", "CC_Charge" ...
        public JObject Parameters { get; set; }
    }

    // 1. 暂停指令
    public class PauseAgingJob
    {
        public string SlotId { get; set; }
    }

    // 2. 继续/恢复指令
    public class ResumeAgingJob
    {
        public string SlotId { get; set; }
    }

    // 3. 停止/删除指令 (终止流程)
    public class StopAgingJob
    {
        public string SlotId { get; set; }
        public string Reason { get; set; }
    }
}