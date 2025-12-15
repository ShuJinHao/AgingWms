using AgingWms.Core.Domain;
using AgingWms.Workflow.Workflows;
using AutoMapper;
using MassTransit;
using Microsoft.Extensions.Caching.Memory; // 引用缓存
using SharedKernel.Repositoy;
using SharedKernel.Workflow.Contracts;
using SharedKernel.Workflow.Workflows;
using System;
using System.Threading.Tasks;

namespace AgingWms.Workflow.Consumers
{
    // =================================================================
    // 【完整版】老化任务全能消费者 (带缓存同步)
    // =================================================================
    public class AgingJobConsumer :
        IConsumer<StartAgingJob>,
        IConsumer<PauseAgingJob>,
        IConsumer<ResumeAgingJob>,
        IConsumer<StopAgingJob>
    {
        private readonly AgingProcessWorkflow _workflowBuilder;
        private readonly IMapper _mapper;
        private readonly IRepository<WarehouseSlot> _repository;
        private readonly IMemoryCache _cache; // 注入缓存

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

        // --- 辅助方法：统一更新缓存 ---
        // 只要调用这个，Activity 下次循环读到的就是最新状态，没有任何延迟
        private void UpdateCache(string slotId, SlotStatus status)
        {
            // Set 会直接覆盖旧值
            _cache.Set($"SlotStatus_{slotId}", status, TimeSpan.FromHours(1));
        }

        // =============================================================
        // 1. 启动任务 (Start)
        // =============================================================
        public async Task Consume(ConsumeContext<StartAgingJob> context)
        {
            var msg = context.Message;
            Console.WriteLine($"[JobConsumer] 收到启动指令 -> 检查库位: {msg.SlotId} ...");

            // --- 安全门禁检测 ---
            var slot = await _repository.GetByIdAsync(msg.SlotId);

            if (slot == null)
            {
                Console.WriteLine($"[JobConsumer] 拒绝启动: 库位 {msg.SlotId} 不存在！");
                return;
            }
            if (string.IsNullOrEmpty(slot.TrayBarcode))
            {
                Console.WriteLine($"[JobConsumer] 拒绝启动: 库位 {msg.SlotId} 无托盘！");
                return;
            }
            // 防止重复启动 (根据业务逻辑，非 Empty/Occupied 不让启动)
            if (slot.Status != SlotStatus.Empty && slot.Status != SlotStatus.Occupied)
            {
                Console.WriteLine($"[JobConsumer] 拒绝启动: 库位状态为 {slot.Status}，不是空闲状态！");
                return;
            }

            // --- 更新数据库 ---
            slot.Status = SlotStatus.Occupied;
            _repository.Update(slot);
            await _repository.SaveChangesAsync();

            // --- 【关键】同步更新缓存 ---
            UpdateCache(slot.SlotId, slot.Status);

            // --- 启动工作流 ---
            var request = _mapper.Map<AgingJobRequest>(msg);
            string trackingId = await _workflowBuilder.RunAsync(request);

            Console.WriteLine($"[JobConsumer] 工作流已启动 -> TrackingID: {trackingId}");
        }

        // =============================================================
        // 2. 暂停任务 (Pause)
        // =============================================================
        public async Task Consume(ConsumeContext<PauseAgingJob> context)
        {
            var slot = await _repository.GetByIdAsync(context.Message.SlotId);

            // 只有在运行/占用状态下才能暂停
            if (slot != null && (slot.Status == SlotStatus.Occupied || slot.Status == SlotStatus.Running))
            {
                // 1. 改库
                slot.Status = SlotStatus.Paused;
                _repository.Update(slot);
                await _repository.SaveChangesAsync();

                // 2. 改缓存 (Activity 会立马读到这个 Paused)
                UpdateCache(slot.SlotId, slot.Status);

                Console.WriteLine($"[JobConsumer] 库位 {slot.SlotId} 已暂停 (Cache Updated)");
            }
        }

        // =============================================================
        // 3. 恢复任务 (Resume)
        // =============================================================
        public async Task Consume(ConsumeContext<ResumeAgingJob> context)
        {
            var slot = await _repository.GetByIdAsync(context.Message.SlotId);

            // 只有在暂停状态下才能恢复
            if (slot != null && slot.Status == SlotStatus.Paused)
            {
                // 1. 改库
                slot.Status = SlotStatus.Occupied; // 恢复为正常状态
                _repository.Update(slot);
                await _repository.SaveChangesAsync();

                // 2. 改缓存 (Activity 会立马读到这个 Occupied，跳出等待循环)
                UpdateCache(slot.SlotId, slot.Status);

                Console.WriteLine($"[JobConsumer] 库位 {slot.SlotId} 已恢复运行 (Cache Updated)");
            }
        }

        // =============================================================
        // 4. 停止任务 (Stop)
        // =============================================================
        public async Task Consume(ConsumeContext<StopAgingJob> context)
        {
            var slot = await _repository.GetByIdAsync(context.Message.SlotId);

            if (slot != null)
            {
                // 1. 改库
                slot.Status = SlotStatus.Error; // 标记为错误/停止
                _repository.Update(slot);
                await _repository.SaveChangesAsync();

                // 2. 改缓存 (Activity 读到这个状态会直接抛异常退出)
                UpdateCache(slot.SlotId, slot.Status);

                Console.WriteLine($"[JobConsumer] 库位 {slot.SlotId} 已强制停止 (原因: {context.Message.Reason})");
            }
        }
    }
}