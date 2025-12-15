using AgingWms.Core.Domain;
using AutoMapper;
using MassTransit;
using SharedKernel.Contracts;
using SharedKernel.Dto;
using SharedKernel.Repositoy;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgingWms.UseCases.Consumers.Wms.Queries
{
    public class SlotQueryConsumer : IConsumer<GetSlotQuery>
    {
        // 注入只读仓储 (Read-Only Interface)
        private readonly IReadRepository<WarehouseSlot> _readRepository;

        private readonly IMapper _mapper;

        public SlotQueryConsumer(IReadRepository<WarehouseSlot> readRepository, IMapper mapper)
        {
            _readRepository = readRepository;
            _mapper = mapper;
        }

        public async Task Consume(ConsumeContext<GetSlotQuery> context)
        {
            try
            {
                var query = context.Message;

                // 1. 查询数据 (使用只读仓储)
                var slotEntity = await _readRepository.GetByIdAsync(query.SlotId);

                if (slotEntity == null)
                {
                    // 2. 未找到：返回失败状态
                    await context.RespondAsync(new SlotQueryResult
                    {
                        IsFound = false,
                        Message = $"库位 {query.SlotId} 不存在",
                        Slot = null
                    });
                    return;
                }

                // 3. 找到数据：使用 AutoMapper 转换为 DTO
                // 这一步会自动处理 WarehouseSlot -> WarehouseSlotDto 的深层映射
                var slotDto = _mapper.Map<WarehouseSlotDto>(slotEntity);

                // 4. 返回成功结果 (DTO)
                await context.RespondAsync(new SlotQueryResult
                {
                    IsFound = true,
                    Message = "查询成功",
                    Slot = slotDto
                });
            }
            catch (Exception ex)
            {
                // 5. 异常处理
                await context.RespondAsync(new SlotQueryResult
                {
                    IsFound = false,
                    Message = $"查询异常: {ex.Message}",
                    Slot = null
                });
            }
        }
    }
}