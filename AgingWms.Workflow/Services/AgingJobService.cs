using AutoMapper;
using MassTransit;
using SharedKernel.Dto; // 引用 OperationResult
using SharedKernel.Workflow.Contracts;
using System.Threading.Tasks;

namespace AgingWms.Workflow.Services
{
    public class AgingJobService
    {
        private readonly IBus _bus;
        private readonly IMapper _mapper;

        public AgingJobService(IBus bus, IMapper mapper)
        {
            _bus = bus;
            _mapper = mapper;
        }

        // 1. 启动任务 (Request/Response)
        public async Task<OperationResult> StartJobAsync(AgingJobDto jobDto)
        {
            var client = _bus.CreateRequestClient<StartAgingJob>();
            var command = _mapper.Map<StartAgingJob>(jobDto);
            // 10秒超时
            var response = await client.GetResponse<OperationResult>(command, timeout: RequestTimeout.After(s: 10));
            return response.Message;
        }

        // 2. 暂停任务 (改为 Request/Response)
        public async Task<OperationResult> PauseJobAsync(string slotId)
        {
            var client = _bus.CreateRequestClient<PauseAgingJob>();
            // 匿名对象自动匹配接口
            var response = await client.GetResponse<OperationResult>(new { SlotId = slotId }, timeout: RequestTimeout.After(s: 10));
            return response.Message;
        }

        // 3. 恢复任务 (改为 Request/Response)
        public async Task<OperationResult> ResumeJobAsync(string slotId)
        {
            var client = _bus.CreateRequestClient<ResumeAgingJob>();
            var response = await client.GetResponse<OperationResult>(new { SlotId = slotId }, timeout: RequestTimeout.After(s: 10));
            return response.Message;
        }

        // 4. 停止任务 (改为 Request/Response)
        public async Task<OperationResult> StopJobAsync(string slotId)
        {
            var client = _bus.CreateRequestClient<StopAgingJob>();
            var response = await client.GetResponse<OperationResult>(new { SlotId = slotId, Reason = "UI Manual Stop" }, timeout: RequestTimeout.After(s: 10));
            return response.Message;
        }
    }
}