using AgingWms.UseCases.Services;
using MassTransit;
using SharedKernel.Contracts;
using System.Threading.Tasks;

namespace AgingWms.Workflow.Consumers
{
    // 专门给 UI 用的消费者，订阅所有库位的广播
    public class DashboardConsumer :
        IConsumer<SlotTelemetryEvent>,
        IConsumer<SlotStepStateEvent>
    {
        private readonly RealTimeMonitorService _monitorService;

        public DashboardConsumer(RealTimeMonitorService monitorService)
        {
            _monitorService = monitorService;
        }

        public Task Consume(ConsumeContext<SlotTelemetryEvent> context)
        {
            // 收到高频电压电流数据 -> 转发给 UI 服务
            _monitorService.NotifyTelemetry(context.Message);
            return Task.CompletedTask;
        }

        public Task Consume(ConsumeContext<SlotStepStateEvent> context)
        {
            // 收到工步开始/结束 -> 转发给 UI 服务
            _monitorService.NotifyStepState(context.Message);
            return Task.CompletedTask;
        }
    }
}