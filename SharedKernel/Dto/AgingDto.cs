using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharedKernel.Dto
{
    // 前端 UI 绑定的模型
    public class AgingJobDto
    {
        public string SlotId { get; set; }
        public string BatchId { get; set; }
        public List<JobStepConfigDto> Steps { get; set; } = new List<JobStepConfigDto>();
    }

    public class JobStepConfigDto
    {
        public string StepType { get; set; }
        public JObject Parameters { get; set; }
    }
}