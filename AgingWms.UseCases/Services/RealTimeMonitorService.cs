using SharedKernel.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgingWms.UseCases.Services
{
    // 单例服务：负责将 MassTransit 的消息转发给 UI
    public class RealTimeMonitorService
    {
        // 定义两个事件，供 UI 订阅
        public event Action<SlotTelemetryEvent>? OnTelemetryReceived;

        public event Action<SlotStepStateEvent>? OnStepStateReceived;

        // 1. 收到遥测数据 (Activity -> Consumer -> Here)
        public void NotifyTelemetry(SlotTelemetryEvent msg)
        {
            OnTelemetryReceived?.Invoke(msg);
        }

        // 2. 收到工步状态变更
        public void NotifyStepState(SlotStepStateEvent msg)
        {
            OnStepStateReceived?.Invoke(msg);
        }
    }
}