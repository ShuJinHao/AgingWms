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
    public class ConstantVoltageChargeActivity : IActivity<CvChargeArguments, CvChargeLog>
    {
        private readonly IReadRepository<WarehouseSlot> _repository;
        private readonly IMemoryCache _cache;

        public ConstantVoltageChargeActivity(IReadRepository<WarehouseSlot> repository, IMemoryCache cache)
        {
            _repository = repository;
            _cache = cache;
        }

        public async Task<ExecutionResult> Execute(ExecuteContext<CvChargeArguments> context)
        {
            var args = context.Arguments;
            var startTime = DateTime.Now;

            await context.Publish<SlotStepStateEvent>(new
            {
                SlotId = args.SlotId,
                EventType = StepEventType.Started,
                StepName = "恒压充",
                ProgressPercent = 0.0,
                RemainingMinutes = args.MaxDurationMinutes,
                Message = $"CV充电启动: {args.TargetVoltage}V",
                Timestamp = DateTime.Now
            });

            double currentCurrent = 5.0; // 初始电流
            double accumulatedAh = 0.0;
            var interval = TimeSpan.FromSeconds(1);
            var elapsed = TimeSpan.Zero;
            var maxDuration = TimeSpan.FromMinutes(args.MaxDurationMinutes);

            while (currentCurrent > args.CutoffCurrent)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var status = await GetStatusAsync(args.SlotId);

                if (status == SlotStatus.Error || status == SlotStatus.Empty) throw new Exception("强制终止");
                if (status == SlotStatus.Paused)
                {
                    await context.Publish<SlotStepStateEvent>(new { SlotId = args.SlotId, EventType = StepEventType.Paused, StepName = "恒压充", Message = "CV暂停", Timestamp = DateTime.Now });
                    await Task.Delay(2000);
                    continue;
                }

                if (elapsed > maxDuration) throw new Exception("恒压超时");

                // 模拟数据
                currentCurrent = currentCurrent * 0.95; // 电流衰减
                if (currentCurrent < 0.01) currentCurrent = 0.01;

                double stepCapacity = currentCurrent * (interval.TotalSeconds / 3600.0);
                accumulatedAh += stepCapacity;

                // 实时遥测
                await context.Publish<SlotTelemetryEvent>(new
                {
                    SlotId = args.SlotId,
                    TrayBarcode = "Unknown",
                    Voltage = args.TargetVoltage, // 恒压不变
                    Current = currentCurrent,
                    Temperature = 35.0,
                    Capacity = accumulatedAh,
                    CurrentStepName = "恒压充CV",
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
                StepName = "恒压充",
                ProgressPercent = 100.0,
                RemainingMinutes = 0.0,
                Message = "CV充电完成",
                Timestamp = DateTime.Now
            });

            return context.Completed(new CvChargeLog { SlotId = args.SlotId, TotalCapacityAh = accumulatedAh, StartTime = startTime, EndTime = DateTime.Now });
        }

        private async Task<SlotStatus> GetStatusAsync(string slotId)
        {
            if (_cache.TryGetValue($"SlotStatus_{slotId}", out SlotStatus status)) return status;
            var slot = await _repository.GetByIdAsync(slotId);
            if (slot == null) return SlotStatus.Empty;
            _cache.Set($"SlotStatus_{slotId}", slot.Status, TimeSpan.FromHours(1));
            return slot.Status;
        }

        public async Task<CompensationResult> Compensate(CompensateContext<CvChargeLog> context)
        {
            await Task.Delay(500);
            return context.Compensated();
        }
    }
}