### Project Status: Architectural PoC (Proof of Concept)

本项目是一个**工业级分布式控制系统的架构原型**。
旨在验证 **.NET 10 + MassTransit (In-Memory)** 在制造业高并发场景下的架构可行性。

> **⚠️ 关于跨平台兼容性的说明 (Compatibility Note):**
>
> - **Control Plane (UI):** 目前演示端采用 **WPF (Windows)** 构建，旨在快速验证业务逻辑与交互模型（Rapid Prototyping）。
> - **Core Architecture:** 核心业务层 (`Core`, `Workflow`, `Infrastructure`) 均基于标准 **.NET 10** 设计，**完全解耦且无 GUI 依赖**。
> - **Future Roadmap:** 生产环境边缘节点 (Edge Nodes) 可无缝迁移至 **Linux (Docker/Headless)** 或 **Avalonia** 跨平台方案。

#### 核心价值 (Core Values):

1. **Routing Slip 编排:** 展示了 MassTransit 路由单模式在动态工艺（如：充放电流程）中的应用。
2. **高并发控制:** 验证了 EF Core 乐观锁 (`RowVersion`) 在多线程资源竞争下的实践。
3. **整洁架构 (Clean Architecture):** 实现了 UI、业务逻辑与基础设施层的严格分离，确保内核的可移植性。

#### Known Limitations (待优化项):

- **状态机解耦:** 目前 `SlotStatus` (资源状态) 与 `WorkflowState` (流程状态) 暂未物理分离，Demo 阶段采用了混合状态管理。生产环境建议拆分为独立的 `ResourceContext`。
- **持久化增强:** 状态流转目前主要依赖内存与数据库快照，建议后续引入 **Saga State Machine** 以支持长周期的分布式事务与自动补偿。
