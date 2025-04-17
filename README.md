# YAESandBox (.NET 版本) - AI 驱动的叙事沙盒引擎 (原型)

本项目是一个实验性的、AI 驱动的叙事沙盒引擎的原型，旨在探索一种基于“叙事块 (Block)”构建和演化交互式故事或游戏世界的方法。其核心理念是让 AI 成为内容生成和状态演变的主要驱动力。

**项目灵感与区别**

*   YAESandBox (全称 `YAESandBox Ain't EraSandBox` 或 `Yet Another EraSandBox`) 最初受到 Era 类游戏的启发。
*   然而，当前 .NET 版本的设计已显著不同，更加专注于**Block 结构**、**并发叙事生成**以及**通过工作流与 AI（目前为模拟）交互**的模式。

**项目状态: 原型阶段 (Prototype Stage)**

**请注意:** 本项目目前仍处于**早期原型阶段**。代码主要用于探索核心架构和功能可行性。你可能会遇到：

*   未完全实现的功能。
*   简化的错误处理 (某些情况下可能导致程序不稳定或需要重启)。
*   未来可能进行大幅重构的代码区域。
*   模拟的 AI 交互逻辑 (WorkflowService 中的具体实现是模拟的)。

**核心理念：AI 作为状态演变的驱动力**

*   系统设计倾向于让 AI（或模拟 AI 的工作流）通过定义好的接口（如原子操作）来驱动世界状态的变化。
*   后端提供基础的状态容器 (`Block`, `WorldState`, `GameState`) 和操作机制，具体的叙事逻辑、规则判断和内容生成委托给外部工作流。
*   目标是创建一个灵活的框架，能够承载由 AI 动态生成和发展的叙事体验。

## 当前架构与核心特性

本项目采用基于 ASP.NET Core 的后端架构，提供 RESTful API 和 SignalR 实时通信能力。

*   **后端技术栈**:
    *   **.NET 9 / C# 13 Preview**: 利用最新的 .NET 平台特性。
    *   **ASP.NET Core**: 构建 Web API 和 SignalR Hub。
    *   **SignalR**: 实现客户端与服务器之间的实时双向通信。

*   **API (RESTful Controllers)**:
    *   `BlocksController`: 查询 Block 列表、单个 Block 详情以及整个 Block 树的拓扑结构。
    *   `EntitiesController`: 查询指定 Block 当前 WorldState 中的实体列表和单个实体详情。
    *   `GameStateController`: 获取和修改指定 Block 的 GameState。
    *   `BlockManagementController`: 提供手动管理 Block（目前只有删除）的 API 端点。
    *   `AtomicController`: 接收并执行对指定 Block 的批量原子化操作 (`AtomicOperation`) 请求，用于精确修改 WorldState。
    *   `PersistenceController`: 提供 `/api/persistence/save` 和 `/api/persistence/load` 端点，用于保存和加载整个应用的状态（包括所有 Block、WorldState 快照、GameState 以及前端盲存数据）。

*   **实时通信 (SignalR `GameHub`)**:
    *   **客户端 -> 服务器:**
        *   `TriggerMainWorkflow`: 触发主工作流，通常创建新的子 Block 并启动异步内容生成/状态更新。
        *   `TriggerMicroWorkflow`: 触发微工作流，不创建 Block，用于更新特定 UI 元素（由 `TargetElementId` 标识）。
        *   `ResolveConflict`: 提交用户对主工作流冲突的解决方案。
    *   **服务器 -> 客户端:**
        *   `ReceiveBlockStatusUpdate`: 通知 Block 状态变化 (Idle, Loading, ResolvingConflict, Error)。
        *   `ReceiveDisplayUpdate`: 推送工作流生成的内容（流式或完整），并通过 `TargetElementId` 区分是更新主 Block 显示区还是特定 UI 元素。使用 `StreamStatus` 指示流状态。
        *   `ReceiveConflictDetected`: 当主工作流完成但检测到与用户修改冲突时发送，包含冲突详情。
        *   `ReceiveStateUpdateSignal`: 轻量级信号，提示客户端某个 Block 的状态可能已更新，鼓励重新获取数据。

*   **核心状态管理 (`BlockManager`)**:
    *   内存中管理所有 `Block` 实例。
    *   每个 `Block` 包含：
        *   `WorldState`: 存储该 Block 时间点的实体状态 (Items, Characters, Places)。包含多个快照 (`wsInput`, `wsPostAI`, `wsPostUser`)，`wsTemp` 用于临时计算。
        *   `GameState`: 存储与 Block 相关的游戏设置（键值对），无快照，一切修改简单应用。
        *   `BlockStatus`: 管理 Block 的生命周期状态 (`Idle`, `Loading`, `ResolvingConflict`, `Error`)，控制并发操作和状态转换。
    *   使用 `AsyncLock` 控制对单个 Block 的并发访问。

*   **工作流 (通过 `WorkflowService` 协调)**:
    *   **主工作流**: 模拟异步 AI 处理，创建子 Block，通过 `DisplayUpdateDto` (`TargetElementId = null`) 流式或一次性更新 Block 内容，并可能生成 `AtomicOperation` 来改变 `WorldState`。完成时可能进入 `Idle`, `Error` 或 `ResolvingConflict` 状态。
    *   **微工作流**: 模拟异步 AI 处理，不创建 Block 或直接修改状态，通过 `DisplayUpdateDto` (包含 `TargetElementId`) 更新特定 UI 元素。

*   **原子化操作 (`AtomicOperation`)**:
    *   定义了对实体的基本操作：`CreateEntity`, `ModifyEntity`, `DeleteEntity`。
    *   可通过 `AtomicController` API 提交，由 `BlockManager` 根据 Block 状态执行或暂存。
    *   是工作流改变 `WorldState` 的主要机制。

*   **持久化**:
    *   `BlockManager` 实现了 `SaveToFileAsync` 和 `LoadFromFileAsync`。
    *   可以将完整的内存状态（所有 Block、状态快照、GameState、元数据、盲存数据）序列化为 JSON 文件，并从文件恢复。
    *   加载时，非 `Idle` 状态的 Block 会被恢复为 `Idle` 状态，但保留其非临时的 `WorldState` 快照。

*   **实体系统**:
    *   核心实体类型: `Item`, `Character`, `Place` (继承自 `BaseEntity`)。
    *   使用 `TypedID` (包含 `EntityType` 和 `EntityId`) 进行类型安全的实体引用。
    *   实体包含一个灵活的属性系统 (`Attributes`)，支持动态添加和修改属性。

## 未来方向与愿景

*   **完善的工作流引擎**: 设计更灵活的工作流配置方式（可能通过可视化界面或脚本），解耦工作流逻辑与核心代码。
*   **真实 AI 集成**: 替换 `WorkflowService` 中的模拟逻辑，接入真正的大语言模型服务 (如 Gemini, Claude, DeepSeek 等)。
*   **前端实现**: 构建用户界面。可以利用本文档和 OpenAPI 规范，**尝试让 AI 辅助生成前端代码**。
*   **更细致的状态同步**: 探索更优化的前端状态同步策略，减少不必要的 API 调用。
*   **错误处理与健壮性**: 增强错误处理机制，提高系统稳定性。特别是 `Error` 状态目前还没有办法退出，对于指令执行返回的报错内容也没有很好的应用（目前一旦有错误就直接切换到 `Error` 锁死）。

## 技术栈总结

*   **后端**: .NET 9, C# 13 Preview, ASP.NET Core, SignalR, System.Text.Json
*   **测试**: xUnit (虽然之前的尝试不完美，但框架已集成)
*   **前端**: *待定* (例如 React, Vue, Blazor WASM 等，可由 AI 辅助启动)