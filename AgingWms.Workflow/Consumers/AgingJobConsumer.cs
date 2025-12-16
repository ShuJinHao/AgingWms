using AgingWms.Core.Domain;
using AgingWms.Workflow.Workflows;
using AutoMapper;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using SharedKernel.Repositoy;
using SharedKernel.Workflow.Contracts;
using SharedKernel.Workflow.Workflows;
using SharedKernel.Contracts;
using SharedKernel.Dto;
using System;
using System.Threading.Tasks;

namespace AgingWms.Workflow.Consumers
{
    public class AgingJobConsumer :
        IConsumer<StartAgingJob>,
        IConsumer<PauseAgingJob>,
        IConsumer<ResumeAgingJob>,
        IConsumer<StopAgingJob>
    {
        private readonly AgingProcessWorkflow _workflowBuilder;
        private readonly IMapper _mapper;
        private readonly IRepository<WarehouseSlot> _repository;
        private readonly IMemoryCache _cache;

        public AgingJobConsumer(
            AgingProcessWorkflow workflowBuilder,
            IMapper mapper,
            IRepository<WarehouseSlot> repository,
            IMemoryCache cache)
        {
            _workflowBuilder = workflowBuilder;
            _mapper = mapper;
            _repository = repository;
            _cache = cache;
        }

        private void UpdateCache(string slotId, SlotStatus status)
        {
            _cache.Set($"SlotStatus_{slotId}", status, TimeSpan.FromHours(1));
        }

        // 1. 启动任务
        public async Task Consume(ConsumeContext<StartAgingJob> context)
        {
            var msg = context.Message;
            var slot = await _repository.GetByIdAsync(msg.SlotId);

            if (slot == null || string.IsNullOrEmpty(slot.TrayBarcode))
            {
                await context.RespondAsync(new OperationResult { IsSuccess = false, Message = "库位无效" });
                return;
            }

            if (slot.Status != SlotStatus.Empty && slot.Status != SlotStatus.Occupied && slot.Status != SlotStatus.Running)
            {
                await context.RespondAsync(new OperationResult { IsSuccess = false, Message = $"状态 {slot.Status} 不允许启动" });
                return;
            }

            try
            {
                // A. 改库状态
                slot.UpdateStatus(SlotStatus.Running);

                // 【启动时】写入"启动中"是合理的，因为这是初始状态
                slot.UpdateCurrentStep("启动中");

                _repository.Update(slot);
                await _repository.SaveChangesAsync();
                UpdateCache(slot.Id, slot.Status);

                var request = _mapper.Map<AgingJobRequest>(msg);
                await _workflowBuilder.RunAsync(request); // 启动工作流

                Console.WriteLine($"[JobConsumer] 启动成功: {slot.Id}");

                await context.Publish<SlotStepStateEvent>(new
                {
                    SlotId = slot.Id,
                    StepName = "启动中",
                    EventType = StepEventType.Started,
                    Message = "启动成功",
                    ProgressPercent = 0.0,
                    Timestamp = DateTime.Now
                });

                await context.RespondAsync(new OperationResult { IsSuccess = true, Message = "启动成功" });
            }
            catch (Exception ex)
            {
                await context.RespondAsync(new OperationResult { IsSuccess = false, Message = ex.Message });
            }
        }

        // 2. 暂停任务
        public async Task Consume(ConsumeContext<PauseAgingJob> context)
        {
            var slot = await _repository.GetByIdAsync(context.Message.SlotId);
            if (slot == null) return;

            try
            {
                slot.UpdateStatus(SlotStatus.Paused);
                // 暂停时不改工步名，保持原样
                _repository.Update(slot);
                await _repository.SaveChangesAsync();
                UpdateCache(slot.Id, slot.Status);

                await context.Publish<SlotStepStateEvent>(new
                {
                    SlotId = slot.Id,
                    StepName = slot.CurrentStep, // 保持原名
                    EventType = StepEventType.Paused,
                    Message = "人工暂停",
                    ProgressPercent = 0.0,
                    Timestamp = DateTime.Now
                });

                await context.RespondAsync(new OperationResult { IsSuccess = true, Message = "暂停成功" });
            }
            catch (Exception ex) { await context.RespondAsync(new OperationResult { IsSuccess = false, Message = ex.Message }); }
        }

        // 3. 恢复任务 【核心修复】
        public async Task Consume(ConsumeContext<ResumeAgingJob> context)
        {
            var slot = await _repository.GetByIdAsync(context.Message.SlotId);
            if (slot == null || slot.Status != SlotStatus.Paused)
            {
                await context.RespondAsync(new OperationResult { IsSuccess = false, Message = "无法恢复" });
                return;
            }

            try
            {
                // A. 只改状态，绝不碰 CurrentStep
                // 既然是恢复，原来的工步名就在数据库里，不需要动，动了反而错
                slot.UpdateStatus(SlotStatus.Running);

                _repository.Update(slot);
                await _repository.SaveChangesAsync();
                UpdateCache(slot.Id, slot.Status);

                // B. 恢复执行 (这里假设 workflow 内部有机制处理 Resume，或者仅仅是重置状态让 Telemetry 继续上报)
                // 如果需要重新 RunAsync，请确保参数正确。这里只负责改状态。

                // C. 通知 UI
                // 【关键】直接读数据库里的 CurrentStep，如果没有就发空，交给 UI 的 Telemetry 去修正
                string dbStep = slot.CurrentStep;

                await context.Publish<SlotStepStateEvent>(new
                {
                    SlotId = slot.Id,
                    StepName = dbStep, // 发送数据库里的真值
                    EventType = StepEventType.Resumed,
                    Message = "任务已恢复",
                    ProgressPercent = 0.0,
                    Timestamp = DateTime.Now
                });

                await context.RespondAsync(new OperationResult { IsSuccess = true, Message = "恢复成功" });
            }
            catch (Exception ex)
            {
                await context.RespondAsync(new OperationResult { IsSuccess = false, Message = ex.Message });
            }
        }

        public async Task Consume(ConsumeContext<StopAgingJob> context)
        {
            // 停止逻辑保持不变...
            var slot = await _repository.GetByIdAsync(context.Message.SlotId);
            if (slot != null)
            {
                slot.UpdateStatus(SlotStatus.Error);
                _repository.Update(slot);
                await _repository.SaveChangesAsync();
                UpdateCache(slot.Id, slot.Status);
                await context.Publish<SlotStepStateEvent>(new { SlotId = slot.Id, StepName = "停止", EventType = StepEventType.Faulted, Message = "强制停止", ProgressPercent = 0.0, Timestamp = DateTime.Now });
                await context.RespondAsync(new OperationResult { IsSuccess = true });
            }
        }
    }
}