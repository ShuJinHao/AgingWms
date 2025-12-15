using AgingWms.Core.Domain;
using AgingWms.Workflow.Workflows;
using AutoMapper;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using SharedKernel.Repositoy;
using SharedKernel.Workflow.Contracts;
using SharedKernel.Workflow.Workflows;
using SharedKernel.Contracts;
using SharedKernel.Dto; // OperationResult
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

            if (slot == null)
            {
                await context.RespondAsync(new OperationResult { IsSuccess = false, Message = $"库位 {msg.SlotId} 不存在" });
                return;
            }
            if (string.IsNullOrEmpty(slot.TrayBarcode))
            {
                await context.RespondAsync(new OperationResult { IsSuccess = false, Message = "库位无托盘" });
                return;
            }
            if (slot.Status != SlotStatus.Empty && slot.Status != SlotStatus.Occupied)
            {
                await context.RespondAsync(new OperationResult { IsSuccess = false, Message = $"状态 {slot.Status} 不允许启动" });
                return;
            }

            try
            {
                slot.UpdateStatus(SlotStatus.Running);
                _repository.Update(slot);
                await _repository.SaveChangesAsync();
                UpdateCache(slot.Id, slot.Status);

                var request = _mapper.Map<AgingJobRequest>(msg);
                string trackingId = await _workflowBuilder.RunAsync(request);

                Console.WriteLine($"[JobConsumer] 启动成功: {trackingId}");

                // 通知 UI 更新状态
                await context.Publish<SlotStepStateEvent>(new
                {
                    SlotId = slot.Id,
                    StepName = "准备中",
                    EventType = StepEventType.Started,
                    Message = "启动成功",
                    ProgressPercent = 0.0,
                    Timestamp = DateTime.Now
                });

                // 回应 Service
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

            if (slot == null)
            {
                await context.RespondAsync(new OperationResult { IsSuccess = false, Message = "库位不存在" });
                return;
            }

            // 只有 占用(Occupied) 或 运行(Running) 才能暂停
            if (slot.Status != SlotStatus.Occupied && slot.Status != SlotStatus.Running)
            {
                await context.RespondAsync(new OperationResult { IsSuccess = false, Message = $"当前状态 {slot.Status} 无法暂停" });
                return;
            }

            try
            {
                slot.UpdateStatus(SlotStatus.Paused);
                _repository.Update(slot);
                await _repository.SaveChangesAsync();
                UpdateCache(slot.Id, slot.Status);

                // 通知 UI
                await context.Publish<SlotStepStateEvent>(new
                {
                    SlotId = slot.Id,
                    StepName = "暂停",
                    EventType = StepEventType.Paused,
                    Message = "人工暂停",
                    ProgressPercent = 0.0,
                    Timestamp = DateTime.Now
                });

                // 回应 Service
                await context.RespondAsync(new OperationResult { IsSuccess = true, Message = "暂停成功" });
            }
            catch (Exception ex)
            {
                await context.RespondAsync(new OperationResult { IsSuccess = false, Message = ex.Message });
            }
        }

        // 3. 恢复任务
        public async Task Consume(ConsumeContext<ResumeAgingJob> context)
        {
            var slot = await _repository.GetByIdAsync(context.Message.SlotId);

            if (slot == null)
            {
                await context.RespondAsync(new OperationResult { IsSuccess = false, Message = "库位不存在" });
                return;
            }

            if (slot.Status != SlotStatus.Paused)
            {
                await context.RespondAsync(new OperationResult { IsSuccess = false, Message = $"当前状态 {slot.Status} 无法恢复" });
                return;
            }

            try
            {
                slot.UpdateStatus(SlotStatus.Running);
                _repository.Update(slot);
                await _repository.SaveChangesAsync();
                UpdateCache(slot.Id, slot.Status);

                await context.Publish<SlotStepStateEvent>(new
                {
                    SlotId = slot.Id,
                    StepName = "恢复",
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

        // 4. 停止任务
        public async Task Consume(ConsumeContext<StopAgingJob> context)
        {
            var slot = await _repository.GetByIdAsync(context.Message.SlotId);

            if (slot == null)
            {
                await context.RespondAsync(new OperationResult { IsSuccess = false, Message = "库位不存在" });
                return;
            }

            try
            {
                slot.UpdateStatus(SlotStatus.Error);
                _repository.Update(slot);
                await _repository.SaveChangesAsync();
                UpdateCache(slot.Id, slot.Status);

                await context.Publish<SlotStepStateEvent>(new
                {
                    SlotId = slot.Id,
                    StepName = "停止",
                    EventType = StepEventType.Faulted,
                    Message = $"强制停止: {context.Message.Reason}",
                    ProgressPercent = 0.0,
                    Timestamp = DateTime.Now
                });

                await context.RespondAsync(new OperationResult { IsSuccess = true, Message = "停止成功" });
            }
            catch (Exception ex)
            {
                await context.RespondAsync(new OperationResult { IsSuccess = false, Message = ex.Message });
            }
        }
    }
}