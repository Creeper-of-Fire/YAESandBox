# YAESandBox (.NET 版本) - AI 驱动的叙事沙盒引擎 (原型)

本项目是一个实验性的、AI 驱动的叙事沙盒引擎的原型，旨在探索一种基于“叙事块 (Block)”构建和演化交互式故事或游戏世界的方法。其核心理念是让 AI 成为内容生成和状态演变的主要驱动力。

**项目灵感与区别**

*   YAESandBox (全称 `YAESandBox Ain't EraSandBox` 或 `Yet Another EraSandBox`) 最初受到 Era 类游戏的启发。
*   然而，当前 .NET 版本的设计已显著不同，更加专注于**Block 结构**、**并发叙事生成**以及**通过工作流与 AI 交互**的模式。

**项目状态: 原型阶段 (Prototype Stage)**

**请注意:** 本项目目前仍处于**早期原型阶段**。代码主要用于探索核心架构和功能可行性。你可能会遇到：

*   未完全实现的功能。
*   简化的错误处理 (某些情况下可能导致程序不稳定或需要重启)。
*   未来可能进行大幅重构的代码区域。

**核心理念：AI 作为状态演变的驱动力**

*   系统设计倾向于让 AI（或模拟 AI 的工作流）通过定义好的接口（如原子操作）来驱动世界状态的变化。
*   后端提供基础的状态容器 (`Block`, `WorldState`, `GameState`) 和操作机制，具体的叙事逻辑、规则判断和内容生成委托给外部工作流。
*   目标是创建一个灵活的框架，能够承载由 AI 动态生成和发展的叙事体验。

## 当前项目进度

项目在原型阶段取得了关键性进展，核心的**工作流引擎**和**基础配置管理**已经基本成型并经过了验证。

*   ✅ **全功能AI配置管理 (已完成前后端):**
    *   **成功实现！** 已完成 `AIConfig` 的完整前后端编辑流程。
    *   **后端:** 提供了对AI服务配置集（`AiConfigurationSet`）进行增、删、改、查的稳定API。
    *   **前端:** 能够动态获取所有可用AI模型（如 `DoubaoAiProcessorConfig`）的 **JSON Schema**，并**自动生成配置表单**，允许用户在界面上轻松添加、编辑和管理不同的AI配置。

*   ✅ **智能并行工作流引擎:**
    *   **核心完成！** 后端引擎能够自动分析步骤间的**数据依赖关系**，构建执行图（DAG），并**并行执行**无依赖关系的任务。
    *   已通过并发AI请求的测试，在控制台原型中得到验证。

*   ✅ **动态工作流配置:**
    *   实现了基于JSON的工作流配置系统，支持通过API进行动态的增删改查。
    *   建立了“步骤-模块”的二级结构，并实现了强大的**变量系统**和**作用域控制 (`OutputMappings`)**。

*   ✅ **可运行的控制台原型:**
    *   已创建一个控制台应用，可以加载JSON配置，完整地执行包含并行步骤的工作流，并成功与外部AI服务交互获取结果。

**简而言之，后端的“发动机”已打造完成，并且通往“可视化驾驶舱”的第一条路（AI配置管理）已经完全铺设成功！**


## 未来方向与愿景

基于已有的 `AIConfig` 前端经验，下一步将是把这种成功的模式推广到工作流的其他部分，最终构建一个完整的可视化编辑器。

*   **前端实现 (最高优先级):**
    *   **目标:** 构建一个基于Vue的**单页面应用 (SPA)** 作为工作流的可视化编辑器。
    *   **核心体验:** 将`AIConfig`的开发模式复用到工作流编辑上。提供一种**“代码即画布”**的体验，用户通过配置模块的输入/输出变量来隐式定义数据流，**而非使用传统的节点连线**。
    *   **智能辅助:** 前端将扮演“实时编译器”的角色，提供变量的**上下文感知自动完成**、**依赖关系高亮**和**实时错误/警告检测**。

*   **完善工作流模块生态:**
    *   开发更多通用的内置模块，特别是用于**数据处理与合并**的模块（如列表操作、字符串拼接等），以支持更复杂的数据流。
    *   甚至也许直接允许用户书写简单的脚本。
    *   将所有模块的行为统一到变量系统中。

*   **细化状态同步与错误处理:**
    *   完善`WebSocket`通信协议，提供更丰富的运行时反馈和更健壮的错误处理机制。( 目前工作流的 `DebugDTO` 还是无用的状态。 )

## 技术栈总结

*   **后端**: .NET 9, C# 13 Preview, ASP.NET Core, SignalR, System.Text.Json
*   **测试**: xUnit
*   **前端**: Vue

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
        *   `ReceiveConflictDetected`: 当主工作流完成但检测到与用户修改冲突时发送。
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

*   **工作流引擎 (Workflow Engine)**:
    *   **核心驱动力**: 系统的核心逻辑处理单元，负责生成内容和状态变更指令。
    *   **声明式与数据驱动**: 通过配置步骤、模块以及它们消费/生产的**变量**来定义逻辑，而非硬编码。
    *   **智能并行调度**: 引擎能自动分析步骤间的**数据依赖关系**，构建执行图（DAG），并**并行执行**无依赖关系的任务，极大地提高了与多个或慢速AI服务交互时的效率。
    *   **强作用域控制**: 通过步骤级的**输出映射 (`OutputMappings`)**，精确控制哪些中间变量可以被发布到全局，避免了变量污染，使复杂工作流的构建更清晰、更可靠。
    *   **无状态与只读**: 引擎本身无状态，对核心游戏世界状态只有只读访问权限，所有状态变更都通过返回**原子化操作 (`AtomicOperation`)** 列表来实现。

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