using SharedKernel.Contracts;      // 出参 OperationResult, GetSlotQuery
using MassTransit;
using SharedKernel.Repositoy;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SharedKernel.Dto;

namespace AgingWms.UseCases.Services
{
    public class SlotCommandService
    {
        // 1. 注入请求客户端 (用来发送并等待结果)
        private readonly IRequestClient<SaveSlotData> _saveClient;

        private readonly IRequestClient<RelocateSlot> _moveClient;
        private readonly IRequestClient<ClearSlot> _removeClient;
        private readonly IRequestClient<GetSlotQuery> _queryClient;

        public SlotCommandService(
            IRequestClient<SaveSlotData> saveClient,
            IRequestClient<RelocateSlot> moveClient,
            IRequestClient<ClearSlot> removeClient,
            IRequestClient<GetSlotQuery> queryClient)
        {
            _saveClient = saveClient;
            _moveClient = moveClient;
            _removeClient = removeClient;
            _queryClient = queryClient;
        }

        // --- 1. 写入 (改为返回 OperationResult) ---
        public async Task<OperationResult> WriteSlotAsync(string slotId, string trayBarcode, List<CellDto> cells)
        {
            // GetResponse<T>: 发送消息，并暂停等待 Consumer 的 RespondAsync<T>
            var response = await _saveClient.GetResponse<OperationResult>(new SaveSlotData
            {
                SlotId = slotId,
                TrayBarcode = trayBarcode,
                Cells = cells,
                OperationTime = DateTime.Now
            });

            return response.Message; // 把 Consumer 返回的结果拿出来
        }

        // --- 2. 迁移 ---
        public async Task<OperationResult> MoveSlotAsync(string sourceId, string targetId)
        {
            var response = await _moveClient.GetResponse<OperationResult>(new RelocateSlot
            {
                SourceSlotId = sourceId,
                TargetSlotId = targetId
            });
            return response.Message;
        }

        // --- 3. 移除 ---
        public async Task<OperationResult> RemoveSlotAsync(string slotId)
        {
            var response = await _removeClient.GetResponse<OperationResult>(new ClearSlot
            {
                SlotId = slotId
            });
            return response.Message;
        }

        // --- 4. 查询 ---
        // 注意：查询现在也建议走 MassTransit，以保持架构一致 (当然直接查库也可以，这里演示走消息)
        public async Task<SlotQueryResult> GetSlotAsync(string slotId)
        {
            var response = await _queryClient.GetResponse<SlotQueryResult>(new GetSlotQuery
            {
                SlotId = slotId
            });
            return response.Message;
        }
    }
}