using AutoMapper;
using MassTransit;
using SharedKernel.Dto;
using SharedKernel.Workflow.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgingWms.Workflow.Services
{
    public class AgingJobService
    {
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IMapper _mapper;

        public AgingJobService(IPublishEndpoint publishEndpoint, IMapper mapper)
        {
            _publishEndpoint = publishEndpoint;
            _mapper = mapper;
        }

        /// <summary>
        /// 发布老化任务 (Fire and Forget)
        /// </summary>
        public async Task StartJobAsync(AgingJobDto jobDto)
        {
            if (jobDto == null) throw new ArgumentNullException(nameof(jobDto));

            // 1. 使用 AutoMapper 自动转换为消息契约
            // 彻底解耦：UI 的 DTO 变了，只需要改 Mapping，不需要改这里的逻辑
            var command = _mapper.Map<StartAgingJob>(jobDto);

            // 2. 发布消息到总线
            // MassTransit 会根据类型自动路由给 AgingJobStarterConsumer
            await _publishEndpoint.Publish(command);
        }

        public async Task PauseJobAsync(string slotId)
        {
            await _publishEndpoint.Publish(new PauseAgingJob { SlotId = slotId });
        }

        public async Task ResumeJobAsync(string slotId)
        {
            await _publishEndpoint.Publish(new ResumeAgingJob { SlotId = slotId });
        }

        public async Task StopJobAsync(string slotId)
        {
            await _publishEndpoint.Publish(new StopAgingJob { SlotId = slotId, Reason = "用户手动终止" });
        }
    }
}