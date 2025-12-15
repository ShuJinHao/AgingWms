using System;
using System.Collections.Generic;
using System.Text;

namespace AgingWms.Core.Domain
{
    /// <summary>
    /// 3. 工部/工步数据 (Process Step)
    /// </summary>
    public class ProcessStepData
    {
        public string StepId { get; set; }    // 业务ID，如 "STEP_01"
        public string StepName { get; set; }  // 如 "CC_Charge"
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        // 【关键】必须初始化，否则直接赋值属性会报错
        public StepMetrics Metrics { get; set; } = new StepMetrics();
    }
}