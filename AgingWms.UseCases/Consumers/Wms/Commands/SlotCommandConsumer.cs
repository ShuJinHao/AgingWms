using AgingWms.Core.Domain;
using AgingWms.UseCases.Services;
using MassTransit;
using SharedKernel.Contracts;
using SharedKernel.Dto; // 引用 OperationResult
using System.Threading.Tasks;
using System;

namespace AgingWms.UseCases.Consumers.Wms.Commands
{
    public class SlotCommandConsumer :
        IConsumer<SaveSlotData>,
        IConsumer<RelocateSlot>,
        IConsumer<ClearSlot>
    {
        // 注入 Service
        private readonly SlotCommandService _service;

        public SlotCommandConsumer(SlotCommandService service)
        {
            _service = service;
        }

        // 1. 处理入库消息
        public async Task Consume(ConsumeContext<SaveSlotData> context)
        {
            var msg = context.Message;

            // 调用 Service 执行逻辑
            var result = await _service.WriteSlotAsync(msg.SlotId, msg.TrayCode, msg.Cells);

            // 【关键】回应 UI，否则 UI 会超时
            await context.RespondAsync(result);
        }

        // 2. 处理移库消息
        public async Task Consume(ConsumeContext<RelocateSlot> context)
        {
            var msg = context.Message;
            var result = await _service.MoveSlotAsync(msg.SlotId, msg.TargetSlotId);

            // 回应 UI
            await context.RespondAsync(result);
        }

        // 3. 处理清库消息
        public async Task Consume(ConsumeContext<ClearSlot> context)
        {
            var result = await _service.RemoveSlotAsync(context.Message.SlotId);

            // 回应 UI
            await context.RespondAsync(result);
        }
    }
}