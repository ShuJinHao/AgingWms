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
    public class RestActivity : IActivity<RestArguments, RestLog>
    {
        private readonly IReadRepository<WarehouseSlot> _repository;
        private readonly IMemoryCache _cache;

        public RestActivity(IReadRepository<WarehouseSlot> repository, IMemoryCache cache)
        {
            _repository = repository;
            _cache = cache;
        }

        public async Task<ExecutionResult> Execute(ExecuteContext<RestArguments> context)
        {
            var args = context.Arguments;
            var startTime = DateTime.Now;
            var targetDuration = TimeSpan.FromMinutes(args.DurationMinutes);

            // 1. 【发布事件】工步开始
            await context.Publish<SlotStepStateEvent>(new
            {
                SlotId = args.SlotId,
                EventType = StepEventType.Started,
                StepName = "静置",
                StepIndex = 0, // 如果能从参数传进来更好，这里演示先写0
                ProgressPercent = 0.0,
                RemainingMinutes = args.DurationMinutes,
                Message = $"静置开始，计划 {args.DurationMinutes} 分钟",
                Timestamp = DateTime.Now
            });

            var elapsed = TimeSpan.Zero;
            var interval = TimeSpan.FromSeconds(1); // 1秒推送一次，给UI减负

            Console.WriteLine($"[工步-静置] {args.SlotId} 启动");

            while (elapsed < targetDuration)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                // --- 状态检查 (暂停/停止逻辑) ---
                var status = await GetStatusAsync(args.SlotId);
                if (status == SlotStatus.Error || status == SlotStatus.Empty) throw new Exception("强制终止");
                if (status == SlotStatus.Paused)
                {
                    // 暂停时发布个状态
                    await context.Publish<SlotStepStateEvent>(new { SlotId = args.SlotId, EventType = StepEventType.Paused, StepName = "静置", Message = "流程已暂停", Timestamp = DateTime.Now });
                    await Task.Delay(2000);
                    continue;
                }
                // ---------------------------

                // 模拟数据
                var random = new Random();
                double currentTemp = 25.0 + (random.NextDouble() * 1.0 - 0.5);

                // 2. 【发布事件】实时遥测数据 (UI 订阅这个来跳动)
                await context.Publish<SlotTelemetryEvent>(new
                {
                    SlotId = args.SlotId,
                    TrayBarcode = "Unknown", // 实际应从参数或查库获取
                    Voltage = 0.0,  // 静置电压可视为0或开路电压
                    Current = 0.0,
                    Temperature = currentTemp,
                    Capacity = 0.0,
                    CurrentStepName = "静置中",
                    RunDuration = elapsed,
                    Timestamp = DateTime.Now
                });

                await Task.Delay(interval);
                elapsed += interval;
            }

            // 3. 【发布事件】工步完成
            await context.Publish<SlotStepStateEvent>(new
            {
                SlotId = args.SlotId,
                EventType = StepEventType.Completed,
                StepName = "静置",
                ProgressPercent = 100.0,
                RemainingMinutes = 0.0,
                Message = "静置完成",
                Timestamp = DateTime.Now
            });

            return context.Completed(new RestLog { SlotId = args.SlotId, StartTime = startTime, EndTime = DateTime.Now });
        }

        private async Task<SlotStatus> GetStatusAsync(string slotId)
        {
            if (_cache.TryGetValue($"SlotStatus_{slotId}", out SlotStatus status)) return status;
            var slot = await _repository.GetByIdAsync(slotId);
            if (slot == null) return SlotStatus.Empty;
            _cache.Set($"SlotStatus_{slotId}", slot.Status, TimeSpan.FromHours(1));
            return slot.Status;
        }

        public async Task<CompensationResult> Compensate(CompensateContext<RestLog> context)
        {
            // 出错时也可以发个 Faulted 事件
            await context.Publish<SlotStepStateEvent>(new { SlotId = context.Log.SlotId, EventType = StepEventType.Faulted, Message = "发生回滚", Timestamp = DateTime.Now });
            await Task.Delay(500);
            return context.Compensated();
        }
    }
}