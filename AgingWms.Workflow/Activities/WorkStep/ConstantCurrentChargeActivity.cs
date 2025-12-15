using AgingWms.Core.Domain;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using SharedKernel.Contracts;
using SharedKernel.Repositoy;
using SharedKernel.Workflow.Activities;
using System;
using System.Threading.Tasks;

namespace AgingWms.Workflow.Activities
{
    public class ConstantCurrentChargeActivity : IActivity<CcChargeArguments, CcChargeLog>
    {
        private readonly IReadRepository<WarehouseSlot> _repository;
        private readonly IMemoryCache _cache;

        public ConstantCurrentChargeActivity(IReadRepository<WarehouseSlot> repository, IMemoryCache cache)
        {
            _repository = repository;
            _cache = cache;
        }

        public async Task<ExecutionResult> Execute(ExecuteContext<CcChargeArguments> context)
        {
            var args = context.Arguments;
            var startTime = DateTime.Now;

            // 1. 发布开始事件
            await context.Publish<SlotStepStateEvent>(new
            {
                SlotId = args.SlotId,
                EventType = StepEventType.Started,
                StepName = "恒流充",
                ProgressPercent = 0.0,
                RemainingMinutes = args.MaxDurationMinutes, // 估算值
                Message = $"CC充电启动: {args.TargetCurrent}A",
                Timestamp = DateTime.Now
            });

            double currentVoltage = 3.20;
            double accumulatedAh = 0.0;
            var interval = TimeSpan.FromSeconds(1); // 1秒一次
            var elapsed = TimeSpan.Zero;
            var maxDuration = TimeSpan.FromMinutes(args.MaxDurationMinutes);

            while (currentVoltage < args.CutoffVoltage)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var status = await GetStatusAsync(args.SlotId);

                if (status == SlotStatus.Error || status == SlotStatus.Empty) throw new Exception("强制终止");
                if (status == SlotStatus.Paused)
                {
                    await context.Publish<SlotStepStateEvent>(new { SlotId = args.SlotId, EventType = StepEventType.Paused, StepName = "恒流充", Message = "充电暂停", Timestamp = DateTime.Now });
                    await Task.Delay(2000);
                    continue;
                }

                if (elapsed > maxDuration) throw new Exception("充电超时");

                // 模拟数据计算
                currentVoltage += 0.02; // 电压上升
                var random = new Random();
                double realCurrent = args.TargetCurrent + (random.NextDouble() * 0.05 - 0.025);
                double currentTemp = 30.0 + (elapsed.TotalMinutes * 0.5);

                double stepCapacity = realCurrent * (interval.TotalSeconds / 3600.0);
                accumulatedAh += stepCapacity;

                // 2. 发布实时遥测
                await context.Publish<SlotTelemetryEvent>(new
                {
                    SlotId = args.SlotId,
                    TrayBarcode = "Unknown",
                    Voltage = currentVoltage,
                    Current = realCurrent,
                    Temperature = currentTemp,
                    Capacity = accumulatedAh,
                    CurrentStepName = "恒流充CC",
                    RunDuration = elapsed,
                    Timestamp = DateTime.Now
                });

                await Task.Delay(interval);
                elapsed += interval;
            }

            // 3. 发布完成事件
            await context.Publish<SlotStepStateEvent>(new
            {
                SlotId = args.SlotId,
                EventType = StepEventType.Completed,
                StepName = "恒流充",
                ProgressPercent = 100.0,
                RemainingMinutes = 0.0,
                Message = $"CC充电完成, 充入 {accumulatedAh:F3}Ah",
                Timestamp = DateTime.Now
            });

            return context.Completed(new CcChargeLog { SlotId = args.SlotId, TotalCapacityAh = accumulatedAh, StartTime = startTime, EndTime = DateTime.Now, EndReason = "Cutoff" });
        }

        private async Task<SlotStatus> GetStatusAsync(string slotId)
        {
            if (_cache.TryGetValue($"SlotStatus_{slotId}", out SlotStatus status)) return status;
            var slot = await _repository.GetByIdAsync(slotId);
            if (slot == null) return SlotStatus.Empty;
            _cache.Set($"SlotStatus_{slotId}", slot.Status, TimeSpan.FromHours(1));
            return slot.Status;
        }

        public async Task<CompensationResult> Compensate(CompensateContext<CcChargeLog> context)
        {
            await context.Publish<SlotStepStateEvent>(new { SlotId = context.Log.SlotId, EventType = StepEventType.Faulted, Message = "充电异常回滚", Timestamp = DateTime.Now });
            await Task.Delay(500);
            return context.Compensated();
        }
    }
}