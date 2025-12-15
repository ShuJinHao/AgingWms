using AgingWms.Workflow.Activities;
using MassTransit;
using Newtonsoft.Json.Linq;
using SharedKernel.Workflow.Activities;
using SharedKernel.Workflow.Workflows;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgingWms.Workflow.Workflows
{
    public class AgingProcessWorkflow
    {
        private readonly IBus _bus;
        private readonly IEndpointNameFormatter _formatter; // 用于自动生成队列名

        public AgingProcessWorkflow(IBus bus, IEndpointNameFormatter formatter)
        {
            _bus = bus;
            _formatter = formatter;
        }

        /// <summary>
        /// 核心方法：构建并启动路由单 (Routing Slip)
        /// </summary>
        public async Task<string> RunAsync(AgingJobRequest request)
        {
            // 1. 创建路由单构建器 (RoutingSlipBuilder)
            // TrackingNumber 相当于 Dapr 的 InstanceId
            var trackingNumber = NewId.NextGuid();
            var builder = new RoutingSlipBuilder(trackingNumber);

            // 2. 遍历前端下发的工步，动态组装 Activity
            foreach (var step in request.Steps)
            {
                switch (step.StepType)
                {
                    case "Rest":
                        AddRestStep(builder, request.SlotId, step.Parameters);
                        break;

                    case "CC_Charge":
                        AddCcChargeStep(builder, request.SlotId, step.Parameters);
                        break;

                    case "CV_Charge":
                        AddCvChargeStep(builder, request.SlotId, step.Parameters);
                        break;

                    case "Discharge":
                        AddDischargeStep(builder, request.SlotId, step.Parameters);
                        break;

                    default:
                        throw new ArgumentException($"未知的工步类型: {step.StepType}");
                }
            }

            // 3. (可选) 添加最终的订阅，比如流程结束发个通知
            // builder.AddSubscription(new Uri("queue:aging-process-completed"), RoutingSlipEvents.Completed);

            // 4. 构建路由单
            var routingSlip = builder.Build();

            // 5. 发射！(Execute)
            // 这一步之后，MassTransit 会自动按顺序调度那些 Activity
            await _bus.Execute(routingSlip);

            return trackingNumber.ToString();
        }

        // --- 下面是各个积木的组装逻辑 ---

        private void AddRestStep(RoutingSlipBuilder builder, string slotId, JObject jsonParams)
        {
            // 1. 获取 Activity 的队列地址
            // MassTransit 默认命名规则: Activity类名 + "_execute"
            // 例如: rest_activity_execute
            var queueName = _formatter.ExecuteActivity<RestActivity, RestArguments>();
            var activityAddress = new Uri($"queue:{queueName}");

            // 2. 解析参数
            var args = jsonParams.ToObject<RestArguments>();
            args.SlotId = slotId; // 强制注入库位ID

            // 3. 添加到链条
            builder.AddActivity(nameof(RestActivity), activityAddress, args);
        }

        private void AddCcChargeStep(RoutingSlipBuilder builder, string slotId, JObject jsonParams)
        {
            var queueName = _formatter.ExecuteActivity<ConstantCurrentChargeActivity, CcChargeArguments>();
            var activityAddress = new Uri($"queue:{queueName}");

            var args = jsonParams.ToObject<CcChargeArguments>();
            args.SlotId = slotId;

            builder.AddActivity(nameof(ConstantCurrentChargeActivity), activityAddress, args);
        }

        private void AddCvChargeStep(RoutingSlipBuilder builder, string slotId, JObject jsonParams)
        {
            var queueName = _formatter.ExecuteActivity<ConstantVoltageChargeActivity, CvChargeArguments>();
            var activityAddress = new Uri($"queue:{queueName}");

            var args = jsonParams.ToObject<CvChargeArguments>();
            args.SlotId = slotId;

            builder.AddActivity(nameof(ConstantVoltageChargeActivity), activityAddress, args);
        }

        private void AddDischargeStep(RoutingSlipBuilder builder, string slotId, JObject jsonParams)
        {
            var queueName = _formatter.ExecuteActivity<DischargeActivity, DischargeArguments>();
            var activityAddress = new Uri($"queue:{queueName}");

            var args = jsonParams.ToObject<DischargeArguments>();
            args.SlotId = slotId;

            builder.AddActivity(nameof(DischargeActivity), activityAddress, args);
        }
    }
}