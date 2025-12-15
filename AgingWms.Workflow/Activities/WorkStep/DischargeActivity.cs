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
    public class DischargeActivity : IActivity<DischargeArguments, DischargeLog>
    {
        private readonly IReadRepository<WarehouseSlot> _repository;
        private readonly IMemoryCache _cache;

        public DischargeActivity(IReadRepository<WarehouseSlot> repository, IMemoryCache cache)
        {
            _repository = repository;
            _cache = cache;
        }

        public async Task<ExecutionResult> Execute(ExecuteContext<DischargeArguments> context)
        {
            var args = context.Arguments;
            var startTime = DateTime.Now;

            await context.Publish<SlotStepStateEvent>(new
            {
                SlotId = args.SlotId,
                EventType = StepEventType.Started,
                StepName = "放电",
                ProgressPercent = 0.0,
                RemainingMinutes = args.MaxDurationMinutes,
                Message = $"放电启动: {args.TargetCurrent}A",
                Timestamp = DateTime.Now
            });

            double currentVoltage = 4.20;
            double accumulatedAh = 0.0;
            var interval = TimeSpan.FromSeconds(1);
            var elapsed = TimeSpan.Zero;
            var maxDuration = TimeSpan.FromMinutes(args.MaxDurationMinutes);

            while (currentVoltage > args.CutoffVoltage)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var status = await GetStatusAsync(args.SlotId);

                if (status == SlotStatus.Error || status == SlotStatus.Empty) throw new Exception("强制终止");
                if (status == SlotStatus.Paused)
                {
                    await context.Publish<SlotStepStateEvent>(new { SlotId = args.SlotId, EventType = StepEventType.Paused, StepName = "放电", Message = "放电暂停", Timestamp = DateTime.Now });
                    await Task.Delay(2000);
                    continue;
                }

                if (elapsed > maxDuration) throw new Exception("放电超时");

                // 模拟数据
                currentVoltage -= 0.05; // 电压下降
                double stepCapacity = args.TargetCurrent * (interval.TotalSeconds / 3600.0);
                accumulatedAh += stepCapacity;

                // 实时遥测
                await context.Publish<SlotTelemetryEvent>(new
                {
                    SlotId = args.SlotId,
                    TrayBarcode = "Unknown",
                    Voltage = currentVoltage,
                    Current = args.TargetCurrent, // 恒流
                    Temperature = 40.0,
                    Capacity = accumulatedAh,
                    CurrentStepName = "放电DC",
                    RunDuration = elapsed,
                    Timestamp = DateTime.Now
                });

                await Task.Delay(interval);
                elapsed += interval;
            }

            await context.Publish<SlotStepStateEvent>(new
            {
                SlotId = args.SlotId,
                EventType = StepEventType.Completed,
                StepName = "放电",
                ProgressPercent = 100.0,
                RemainingMinutes = 0.0,
                Message = $"放电完成, 放出 {accumulatedAh:F3}Ah",
                Timestamp = DateTime.Now
            });

            return context.Completed(new DischargeLog { SlotId = args.SlotId, TotalDischargedAh = accumulatedAh, StartTime = startTime, EndTime = DateTime.Now });
        }

        private async Task<SlotStatus> GetStatusAsync(string slotId)
        {
            if (_cache.TryGetValue($"SlotStatus_{slotId}", out SlotStatus status)) return status;
            var slot = await _repository.GetByIdAsync(slotId);
            if (slot == null) return SlotStatus.Empty;
            _cache.Set($"SlotStatus_{slotId}", slot.Status, TimeSpan.FromHours(1));
            return slot.Status;
        }

        public async Task<CompensationResult> Compensate(CompensateContext<DischargeLog> context)
        {
            await Task.Delay(500);
            return context.Compensated();
        }
    }
}