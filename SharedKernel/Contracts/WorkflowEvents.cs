using System;
using System.Collections.Generic;
using System.Text;

namespace SharedKernel.Contracts
{
    // ==========================================
    // 1. 实时遥测事件 (高频发布，如 1秒/次)
    // ==========================================
    public interface SlotTelemetryEvent
    {
        string SlotId { get; }
        string TrayBarcode { get; }

        // 实时电气数据
        double Voltage { get; }      // 当前电压 (V)

        double Current { get; }      // 当前电流 (A)
        double Temperature { get; }  // 当前温度 (°C)
        double Capacity { get; }     // 当前累计容量 (Ah)

        // 实时进度数据
        string CurrentStepName { get; } // 当前工步名称 (如: "恒流充")

        TimeSpan RunDuration { get; }   // 已运行时间
        DateTime Timestamp { get; }     // 数据产生时间
    }

    // ==========================================
    // 2. 工步状态变更事件 (低频发布，如 开始/结束/报错)
    // ==========================================
    public interface SlotStepStateEvent
    {
        string SlotId { get; }

        // 状态类型
        StepEventType EventType { get; }

        string StepName { get; }     // 工步名称
        int StepIndex { get; }       // 第几步

        // 进度信息
        double ProgressPercent { get; } // 进度百分比 (0-100)

        double RemainingMinutes { get; }// 预计剩余时间

        string Message { get; }      // 附加消息 (如 "工步启动", "异常: 温度过高")
        DateTime Timestamp { get; }
    }

    // 事件类型枚举
    public enum StepEventType
    {
        Started = 0,    // 工步开始
        Running = 1,    // 运行中 (可选，用于更新进度条)
        Completed = 2,  // 工步完成 (此时会写数据库)
        Faulted = 3,    // 工步出错
        Paused = 4,     // 暂停
        Resumed = 5     // 恢复
    }
}