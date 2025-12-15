using System;
using System.Collections.Generic;
using System.Text;

namespace SharedKernel.Dto
{
    public class CellDto
    {
        public string Barcode { get; set; }
        public int ChannelIndex { get; set; }
        public bool IsNg { get; set; }
    }

    // 4. 开始流程指令 (给发布服务用)
    public class StartSlotWorkflowCommand
    {
        public string SlotId { get; set; }
        public List<string> Steps { get; set; } = new List<string>();
    }

    // 【新增】通用操作结果
    public class OperationResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
    }

    public class SlotQueryResult
    {
        public bool IsFound { get; set; }
        public string Message { get; set; }
        public WarehouseSlotDto Slot { get; set; }
    }

    // ==========================================
    // 2. 完整的数据传输对象 (DTOs) - 用于读操作
    // ==========================================

    /// <summary>
    /// 库位 DTO (最外层)
    /// </summary>
    public class WarehouseSlotDto
    {
        public string SlotId { get; set; }
        public string SlotName { get; set; }

        // 对应 SlotStatus 枚举 (0:Empty, 1:Occupied, 2:Locked, 99:Error)
        public int Status { get; set; }

        public string TrayBarcode { get; set; }
        public DateTime LastUpdatedTime { get; set; }

        // 嵌套的电芯 DTO 列表
        public List<BatteryCellDto> Cells { get; set; } = new List<BatteryCellDto>();
    }

    /// <summary>
    /// 电芯 DTO (中间层)
    /// </summary>
    public class BatteryCellDto
    {
        public string Barcode { get; set; }
        public int ChannelIndex { get; set; }
        public bool IsNg { get; set; }

        // 嵌套的工步数据列表 (N个工步)
        public List<ProcessStepDto> ProcessSteps { get; set; } = new List<ProcessStepDto>();
    }

    /// <summary>
    /// 工步 DTO (数据层)
    /// </summary>
    public class ProcessStepDto
    {
        public string StepId { get; set; }
        public string StepName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        // 工步详细指标
        public StepMetricsDto Metrics { get; set; } = new StepMetricsDto();
    }

    /// <summary>
    /// 详细指标 DTO (最底层)
    /// </summary>
    public class StepMetricsDto
    {
        public double StartVoltage { get; set; }
        public double EndVoltage { get; set; }
        public double AvgTemperature { get; set; }
        public double MaxTemperature { get; set; }
        public double AvgCurrent { get; set; }
        public double Capacity { get; set; }
    }
}