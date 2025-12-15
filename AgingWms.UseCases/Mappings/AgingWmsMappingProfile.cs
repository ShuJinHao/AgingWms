using AgingWms.Core.Domain;
using AutoMapper;
using SharedKernel.Contracts;
using SharedKernel.Dto;
using SharedKernel.Workflow.Contracts;
using SharedKernel.Workflow.Workflows;

namespace AgingWms.UseCases.Mappings
{
    public class AgingWmsMappingProfile : Profile
    {
        public AgingWmsMappingProfile()
        {
            // =========================================================
            // 1. 读操作映射 (Entity -> DTO)
            // =========================================================
            CreateMap<StepMetrics, StepMetricsDto>().ReverseMap();

            CreateMap<ProcessStepData, ProcessStepDto>()
                .ForMember(dest => dest.Metrics, opt => opt.MapFrom(src => src.Metrics))
                .ReverseMap();

            CreateMap<BatteryCell, BatteryCellDto>()
                .ForMember(dest => dest.Barcode, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.ProcessSteps, opt => opt.MapFrom(src => src.ProcessSteps))
                .ReverseMap();

            CreateMap<WarehouseSlot, WarehouseSlotDto>()
                .ForMember(dest => dest.SlotId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (int)src.Status))
                // 【核心修复】显式映射托盘条码
                // 如果 DTO 里叫 TrayCode，这里就是 .ForMember(dest => dest.TrayCode, ...)
                // 如果 DTO 里也叫 TrayBarcode，AutoMapper 理论上会自动匹配，但加上这行最保险
                .ForMember(dest => dest.TrayBarcode, opt => opt.MapFrom(src => src.TrayBarcode))
                .ForMember(dest => dest.Cells, opt => opt.MapFrom(src => src.Cells));

            // =========================================================
            // 2. 写操作映射 (DTO -> Entity)
            // =========================================================
            CreateMap<CellDto, BatteryCell>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Barcode))
                .ForMember(dest => dest.Barcode, opt => opt.Ignore())
                .ForMember(dest => dest.ProcessSteps, opt => opt.Ignore())
                .ForMember(dest => dest.ChannelIndex, opt => opt.MapFrom(src => src.ChannelIndex))
                .ForMember(dest => dest.IsNg, opt => opt.MapFrom(src => src.IsNg));

            CreateMap<WarehouseSlotDto, WarehouseSlot>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.SlotId))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (SlotStatus)src.Status))
                // 【核心修复】写映射也加上
                .ForMember(dest => dest.TrayBarcode, opt => opt.MapFrom(src => src.TrayBarcode));

            // =========================================================
            // 3. 工作流相关映射
            // =========================================================
            CreateMap<JobStepConfigDto, JobStepConfig>(); // 必不可少
            CreateMap<JobStepConfigDto, WorkflowStepConfig>();
            CreateMap<JobStepConfig, WorkflowStepConfig>();

            CreateMap<StartAgingJob, AgingJobRequest>()
                .ForMember(dest => dest.Steps, opt => opt.MapFrom(src => src.Steps));

            CreateMap<AgingJobDto, StartAgingJob>()
                .ForMember(dest => dest.Steps, opt => opt.MapFrom(src => src.Steps));
        }
    }
}