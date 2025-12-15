using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharedKernel.Workflow.Workflows
{
    // ==========================================
    // 1. 工作流定义的入参模型 (对应前端发的 JSON)
    // ==========================================
    public class AgingJobRequest
    {
        public string SlotId { get; set; }
        public string BatchId { get; set; } // 批次号

        // 工步清单：前端按顺序下发 ["静置", "恒流充", "静置"]
        public List<WorkflowStepConfig> Steps { get; set; } = new List<WorkflowStepConfig>();
    }

    public class WorkflowStepConfig
    {
        public string StepType { get; set; } // "Rest", "CC_Charge", "CV_Charge", "Discharge"
        public JObject Parameters { get; set; } // 动态参数 { "DurationMinutes": 10, ... }
    }
}