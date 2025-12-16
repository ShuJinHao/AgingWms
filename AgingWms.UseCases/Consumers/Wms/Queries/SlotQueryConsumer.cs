using AgingWms.Core.Domain;
using AgingWms.UseCases.Services.DB;
using AutoMapper;
using MassTransit;
using SharedKernel.Contracts;
using SharedKernel.Dto;
using System;
using System.Threading.Tasks;

namespace AgingWms.UseCases.Consumers.Wms.Queries
{
    public class SlotQueryConsumer : IConsumer<GetSlotQuery>
    {
        // 【核心修改】注入 Service，而不是直接注入 Repository
        // 保持架构一致性：Consumer -> Service -> Repository
        private readonly SlotCommandService _service;

        private readonly IMapper _mapper;

        public SlotQueryConsumer(SlotCommandService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        public async Task Consume(ConsumeContext<GetSlotQuery> context)
        {
            try
            {
                var query = context.Message;

                // 1. 调用 Service 获取实体
                // (GetAsync 是 ResourceControlService 基类提供的方法)
                var slotEntity = await _service.GetAsync(query.SlotId);

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
                // Mapping 属于“接口适配”逻辑，放在 Consumer 层是正确的
                var slotDto = _mapper.Map<WarehouseSlotDto>(slotEntity);

                // 4. 返回成功结果
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