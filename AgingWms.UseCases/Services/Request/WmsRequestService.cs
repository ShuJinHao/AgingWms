using MassTransit;
using SharedKernel.Contracts;
using SharedKernel.Dto;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgingWms.UseCases.Services.Request
{
    /// <summary>
    /// WMS 请求服务 (Facade)
    /// 职责：位于 UseCases 层，作为发令官，负责把具体的业务操作转换成 MassTransit 消息发出去
    /// </summary>
    public class WmsRequestService
    {
        private readonly IBus _bus;

        public WmsRequestService(IBus bus)
        {
            _bus = bus;
        }

        // 1. 入库请求
        public async Task<OperationResult> WriteSlotAsync(string slotId, string trayCode, List<CellDto> cells)
        {
            var client = _bus.CreateRequestClient<SaveSlotData>();

            var response = await client.GetResponse<OperationResult>(new
            {
                SlotId = slotId,
                SlotName = slotId,
                TrayCode = trayCode,
                Cells = cells ?? new List<CellDto>(),
                DataJson = ""
            }, timeout: RequestTimeout.After(s: 10));

            return response.Message;
        }

        // 2. 移库请求
        public async Task<OperationResult> MoveSlotAsync(string sourceId, string targetId)
        {
            var client = _bus.CreateRequestClient<RelocateSlot>();

            var response = await client.GetResponse<OperationResult>(new
            {
                SlotId = sourceId,
                TargetSlotId = targetId
            }, timeout: RequestTimeout.After(s: 10));

            return response.Message;
        }

        // 3. 移除请求
        public async Task<OperationResult> RemoveSlotAsync(string slotId)
        {
            var client = _bus.CreateRequestClient<ClearSlot>();

            var response = await client.GetResponse<OperationResult>(new
            {
                SlotId = slotId
            }, timeout: RequestTimeout.After(s: 10));

            return response.Message;
        }
    }
}