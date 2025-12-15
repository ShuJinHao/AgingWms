using AgingWms.Core.Domain;
using AutoMapper;
using SharedKernel.Contracts;
using SharedKernel.Dto;
using SharedKernel.Workflow.Contracts;
using SharedKernel.Workflow.Workflows; // 引用 SharedKernel 里的 DTOs

namespace AgingWms.UseCases.Mappings
{
    public class AgingWmsMappingProfile : Profile
    {
        public AgingWmsMappingProfile()
        {
            // =========================================================
            // 1. 读操作映射 (Entity -> SharedKernel DTO)
            // 用于查询时将数据库实体转换为前端展示数据
            // =========================================================

            // 1.1 基础指标
            CreateMap<StepMetrics, StepMetricsDto>()
                .ReverseMap();

            // 1.2 工步数据
            CreateMap<ProcessStepData, ProcessStepDto>()
                .ForMember(dest => dest.Metrics, opt => opt.MapFrom(src => src.Metrics))
                .ReverseMap();

            // 1.3 电芯数据 (读操作：包含工步详情)
            CreateMap<BatteryCell, BatteryCellDto>()
                .ForMember(dest => dest.ProcessSteps, opt => opt.MapFrom(src => src.ProcessSteps))
                .ReverseMap();

            // 1.4 库位数据 (聚合根)
            CreateMap<WarehouseSlot, WarehouseSlotDto>()
                // 将枚举 Status 转为 int
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (int)src.Status))
                // 嵌套映射电芯列表
                .ForMember(dest => dest.Cells, opt => opt.MapFrom(src => src.Cells))
                .ReverseMap()
                // 反向映射：int 转枚举
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => (SlotStatus)src.Status));

            // =========================================================
            // 2. 写操作映射 (入参 DTO -> Entity)
            // 用于入库/更新时，将简单的入参转换为数据库实体
            // =========================================================

            // 【关键修复】: 解决 List<CellDto> -> List<BatteryCell> 映射报错
            // SharedKernel.Contracts.CellDto 通常只包含条码、位置、NG状态
            CreateMap<CellDto, BatteryCell>()
                // 写入时通常没有工步数据，忽略它以防空指针或覆盖
                .ForMember(dest => dest.ProcessSteps, opt => opt.Ignore())
                // 显式映射基础字段 (虽然名字相同 AutoMapper 会自动处理，但显式写出更安全)
                .ForMember(dest => dest.Barcode, opt => opt.MapFrom(src => src.Barcode))
                .ForMember(dest => dest.ChannelIndex, opt => opt.MapFrom(src => src.ChannelIndex))
                .ForMember(dest => dest.IsNg, opt => opt.MapFrom(src => src.IsNg));

            // 1. 子项映射 (JobStepConfig -> WorkflowStepConfig)
            // 属性名完全一致，AutoMapper 会自动匹配
            CreateMap<JobStepConfig, WorkflowStepConfig>();

            // 2. 主体映射 (StartAgingJob -> AgingJobRequest)
            // Steps 列表会自动使用上面的规则进行转换
            CreateMap<StartAgingJob, AgingJobRequest>();

            CreateMap<JobStepConfigDto, JobStepConfig>(); // 子项
            CreateMap<AgingJobDto, StartAgingJob>();      // 主项
        }
    }
}