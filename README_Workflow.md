# YAESandBox 工作流引擎

## 概述 (Overview)

YAESandBox 工作流引擎是系统核心逻辑的处理单元。它负责根据预定义的配置（工作流和步骤），按顺序执行一系列操作，处理输入信息，并生成原子化指令和用于最终呈现的原始文本 (`raw_text`)。引擎本身是**无状态**的，并且对核心游戏状态（WorldState, GameState）只有**只读**访问权限。

## 核心概念 (Core Concepts)

*   **工作流 (Workflow):** 一个由多个按顺序执行的“步骤”组成的配置。它定义了处理特定任务（如生成故事段落、响应用户交互）的完整逻辑流程。
*   **步骤 (Step):** 一个`步骤`类似于SillyTavern的一个`预设`，是工作流的基本执行单元。每个步骤是模块化、可配置的，包含具体的处理逻辑。步骤可以被多个不同的工作流复用。
*   **输入 (Input):** 工作流执行时可以访问的只读信息源，主要包括：
    *   触发工作流时传递的参数 (Params)。
    *   通过 C# 后端提供的 API 查询到的 WorldState (WS) 和 GameState (GS) 信息。
    *   父 Block 链提供的历史信息（例如历史 `raw_text`，通过 API 获取）。
*   **内部变量 (Internal Variables):** 工作流在执行期间，可以在其内存中维护一套临时的、可读写的变量。这些变量用于在步骤之间传递数据、存储中间结果或控制流程。**这些变量不会被持久化。**
*   **输出 (Output):** 工作流执行完成后产生的主要结果：
    *   **原子化指令列表 (`AtomicOperation[]`):** 一系列描述如何修改 WorldState 或 GameState 的指令。这些指令**不会**被引擎直接执行，而是返回给调用者（`WorkflowService`），最终由 `BlockManager` 根据工作流类型（主/微工作流）和当前 Block 状态决定如何应用。
    *   **原始文本 (`raw_text`):** 工作流执行产生的主要文本结果，通常用于存档、历史记录，并作为最终显示内容的基础。

## 工作流执行流程 (Workflow Execution Flow)

1.  **触发:** 由外部服务 (`WorkflowService`) 根据用户操作或系统事件，选择一个工作流配置并启动引擎实例。输入参数被传递给引擎。
2.  **初始化:** 引擎创建临时的内部变量上下文。
3.  **顺序执行步骤:** 引擎按照工作流配置中定义的顺序，依次执行每一个步骤。
4.  **步骤执行:** 每个步骤内部执行其定义的逻辑（见下文“步骤详解”）。步骤可以读取输入、读写内部变量、生成原子化指令（暂存）。
5.  **指令累积:** 步骤生成的原子化指令被添加到工作流实例持有的一个临时列表中。
6.  **完成:** 所有步骤执行完毕后，引擎将累积的原子化指令列表和最终生成的 `raw_text` 作为结果返回给 `WorkflowService`。
7.  **清理:** 引擎实例销毁，内部变量被丢弃。

## 步骤详解 (Step Details)

一个“步骤 (Step)”是工作流的基本构建块和执行单元。它代表工作流中的一个处理阶段。步骤是模块化的，可以独立配置和存储，并被不同的工作流引用。

步骤的核心是围绕一个**主处理脚本 (Main Processing Script)** 来组织的，并根据是否涉及 AI 交互，其执行流程略有不同。

**涉及 AI 交互的步骤:**

1.  **提示词准备 (Prompt Preparation):**
    *   此阶段负责构建发送给 AI 的提示词 (`prompt_text`)。
    *   它由一系列可配置的“提示词模块”和“脚本模块”组成。
        *   **提示词模块:** 提供文本片段，支持 `{{variable_name}}` 模板。
        *   **脚本模块:** **(轻量级)** 主要用于准备提示词所需的数据，可以读写内部变量，查询 WS/GS (只读)，但不推荐在此生成核心指令。
    *   引擎按顺序执行这些模块，组装出最终的 `prompt_text`。

2.  **AI 请求与流式处理 (AI Request & Stream Processing):**
    *   **AI 调用协调 (由 C# 负责):** C# 后端接收 `prompt_text`，请求调用外部 AI 服务。
    *   **流处理回调脚本 (Stream Processing Callback Script - 可选且独立):**
        *   **仅当 AI 服务以流式方式返回响应时，** C# 后端在接收到**新的文本块 (`chunk`)** 时，会调用为此步骤配置的**这个特定脚本**。
        *   **输入:** `step_execution_time`, `ai_stream_output_added`, `ai_stream_output_total`, 内部变量 (可读写)。
        *   **职责:** 主要负责**生成并请求发送**用于实时前端更新的 `DisplayUpdateDto` (含 `Content`, `ScriptId/Script`, `StreamingStatus=Streaming`)。应避免复杂逻辑。

3.  **主处理脚本执行 (Main Processing Script Execution):**
    *   **触发时机:** 在 AI 调用**完全结束后** (无论是否流式，成功或失败)，C# 后端会调用步骤配置的**主处理脚本**。
    *   **输入:**
        *   工作流内部变量 (可读写)。
        *   `step_execution_time`: 整个步骤（包括 AI 等待）的总耗时。
        *   `ai_final_output`: AI 的最终完整输出（流式则为 `ai_stream_output_total` 的最终值）。
    *   **职责 (核心):**
        *   解析 `ai_final_output`。
        *   执行核心业务逻辑。
        *   修改内部变量。
        *   **生成本步骤主要的 `AtomicOperation`** 并添加到工作流暂存区。
        *   (可选) 请求发送最终的 `DisplayUpdateDto`。

**纯脚本步骤 (No AI Interaction):**

此类步骤**直接跳过**上述的“提示词准备”和“AI 请求与流式处理”阶段。

1.  **主处理脚本执行 (Main Processing Script Execution):**
    *   **触发时机:** 步骤开始执行时，直接调用其配置的**主处理脚本**。
    *   **输入:**
        *   工作流内部变量 (可读写)。
        *   `step_execution_time`: 脚本执行耗时。
        *   `ai_final_output`: 通常为空或 null。
    *   **职责 (核心):**
        *   执行核心业务逻辑。
        *   修改内部变量。
        *   **生成本步骤主要的 `AtomicOperation`** 并添加到工作流暂存区。
        *   (可选) 请求发送 `DisplayUpdateDto`。

**关键简化点:**

*   **核心在主处理脚本:** 无论是 AI 步骤还是纯脚本步骤，最终的核心逻辑和指令生成都集中在“主处理脚本”中。纯脚本步骤可以看作是 `ai_final_output` 为空的 AI 步骤的特例。
*   **准备阶段简化:** 提示词准备阶段的脚本模块应聚焦于数据准备，而非核心逻辑。
*   **流处理回调的独立性:** 它是一个可选的、专门用于处理实时显示的脚本，与主处理逻辑分离。

## 与系统其他部分的交互 (Interaction with Other System Parts)

工作流引擎专注于**纯粹的逻辑执行和结果生成**，并与系统其他部分明确分工：

*   **`WorkflowService` (C#):**
    *   负责**触发**工作流引擎。
    *   向引擎提供初始输入和访问 WS/GS 的 API 接口、以及访问历史记录的API接口之类的。
    *   接收引擎返回的 `AtomicOperation[]` 和 `raw_text`。
    *   协调对外部 AI 服务的调用（根据引擎请求）。
    *   根据引擎请求，调用 `INotifierService` 发送中间的 `DisplayUpdateDto`。
    *   将最终结果传递给 `BlockManager`。
*   **`BlockManager` (C#):**
    *   负责 Block 的创建、状态管理 (Idle, Loading, Conflict, Error) 和 WorldState 快照 (Input, Temp, PostAI, PostUser) 的生命周期。
    *   根据工作流类型（一等/二等公民）和 Block 状态，**最终决定如何应用** `WorkflowService` 传递过来的 `AtomicOperation[]`。处理冲突检测和解决流程。
*   **`INotifierService` (C#):**
    *   负责通过 SignalR 将 `DisplayUpdateDto`, `BlockStatusUpdateDto`, `StateUpdateSignalDto`, `ConflictDetectedDto` 等消息**实际推送**给前端。
*   **外部 AI 服务 (例如 Python):**
    *   提供独立的 AI 计算能力（文本生成、NLP 处理等）。
    *   由 C# 后端按需调用，接收输入，返回结果。**不直接与工作流引擎或前端交互。**

## 设计原则 (Design Principles)

*   **单一职责:** 引擎专注于工作流逻辑执行，不负责状态管理、网络通信或指令的最终应用。
*   **无状态与只读:** 引擎核心不持久化状态，对外部状态（WS/GS）只有只读访问权，保证了执行的可预测性和安全性。
*   **模块化:** 工作流由可复用的步骤组成，易于配置和维护。
*   **灵活性:** 不对工作流的具体实现方式（如必须有“核心文本”变量）做过多硬性规定，允许用户自由组合步骤和变量。


# YAESandBox 工作流引擎接口与配置

## 引擎执行时输入 (Engine Execution Inputs)

当 `WorkflowService` 触发一个工作流执行时，需要向引擎提供以下输入：

1.  **工作流配置 (Workflow Configuration):**
    *   标识要执行的工作流。
    *   包含按顺序执行的步骤 (Step) 列表/引用。
    *   (可能包含) 工作流级别的元数据或设置。

2.  **触发参数 (Trigger Parameters):**
    *   一个字典 (`Dictionary<string, object?>`)，包含从前端或其他触发源传递过来的初始参数。
    *   工作流配置应**声明**其期望的参数及其类型（用于文档或前端提示），但引擎本身可能不强制校验。

3.  **数据查询委托 (`Func<>` Delegates - Data Access API):**
    *   引擎（及其脚本）**不直接访问**数据库或内存状态。它通过 C# 提供的委托来请求数据。这些委托封装了 API 调用或对 `BlockManager` 的查询。
    *   **必要的委托示例:**
        *   `Func<string /*entityId*/, EntityType, Task<BaseEntity?>> GetEntityAsync`: 获取单个实体信息。
        *   `Func<string /*blockId*/, Task<IEnumerable<BaseEntity>?>> GetAllEntitiesAsync`: 获取指定 Block 的所有实体摘要。
        *   `Func<string /*blockId*/, Task<GameState?>> GetGameStateAsync`: 获取指定 Block 的 GameState。
        *   `Func<string /*blockId*/, Task<string?>> GetBlockRawTextAsync`: 获取指定 Block 的 `raw_text` (用于读取历史记录)。
        *   `Func<string /*blockId*/, Task<List<string>>> GetParentBlockIdsAsync`: 获取从根到指定 Block 的父链 ID 列表。
        *   `Func<string /*key*/, Task<object?>> GetPlayerMetadataAsync`: (如果需要 Meta 游戏) 获取玩家相关的元数据、甚至有可能包括计算机识别码之类的。（当然也有可能是脚本自身通过其他方式获取这些数据）
        *   ... 其他根据需要定义的查询委托。
    *   **注意:** 这些委托应设计为**只读**操作。

4.  **显示更新委托 (`Action<>` Delegate - Display Update Request):**
    *   引擎（及其脚本）通过调用此委托来**请求** C# 发送一个**中间的**显示更新给前端。
    *   **委托签名示例:** `Action<DisplayUpdateRequestPayload>`
    *   **`DisplayUpdateRequestPayload` 结构:**
        *   `Content`: (string) 要显示的内容 (文本、HTML 片段、JSON 等)。
        *   `UpdateMode`: (UpdateMode Enum - `FullSnapshot` 或 `Incremental`) 内容是替换还是增量。
        *   `ScriptId`: (string, 可选) 用于渲染 `Content` 的脚本 ID。
        *   `IncrementalType`: (string, 可选) 如果 `UpdateMode` 是 `Incremental`，则指定增量类型 (如 "json-patch")。
    *   **注意:** 调用此委托**不保证**消息立即发送或一定成功，它只是向 C# 发出请求。C# 的 `INotifierService` 负责实际的 SignalR 推送，并会填充 `DisplayUpdateDto` 的其他字段 (如 `RequestId`, `StreamingStatus`)。

## 引擎执行后输出 (Engine Execution Outputs)

当工作流执行完成后，引擎需要返回以下结果给 `WorkflowService`:

1.  **原子化指令列表 (`List<AtomicOperation>`):**
    *   在工作流执行期间累积的所有 `AtomicOperation`。
    *   `WorkflowService` 会将此列表传递给 `BlockManager` 进行后续处理。

2.  **原始文本 (`string raw_text`):**
    *   工作流产生的主要文本结果。
    *   用于存档、历史记录，并作为最终显示的基础。
    *   `WorkflowService` 会将其设置到对应的 Block (`Block.BlockContent`)。

## 工作流/步骤配置 (Workflow/Step Configuration)

除了执行时的输入输出，工作流和步骤本身也需要配置：

1.  **工作流配置:**
    *   **ID/名称:** 唯一标识符。
    *   **步骤列表:** 按顺序引用的步骤配置 ID/名称。
    *   **声明的输入参数:** (文档性质) 描述此工作流期望接收哪些触发参数。
    *   **最终显示脚本 ID (`FinalDisplayScriptId`, 可选):** 引用一个用于渲染此工作流产生的 `raw_text` 的显示脚本。这个脚本在 Block 加载时或历史记录查看时使用。如果未指定，可能使用默认渲染方式。

2.  **步骤配置:**
    *   **ID/名称:** 唯一标识符。
    *   **提示词准备模块列表 (可选):** 定义如何构建 AI 提示。
    *   **AI 服务标识 (可选):** 指定此步骤需要调用哪个外部 AI 服务（如果需要）。
    *   **流式处理回调脚本配置 (可选):** 如果涉及流式 AI，配置用于处理流块的脚本。
    *   **主处理脚本配置:** 配置核心逻辑处理脚本。
    *   **是否流式 (`IsStreaming`, 内部标识):** 步骤配置需要知道其涉及的 AI 调用是否是流式的，以便引擎和 C# 协调器知道是否需要处理流式回调。

## 其他说明 (Other Notes)

*   **流式行为:** 一个工作流中可以混合包含流式和非流式步骤。引擎和 C# 协调器需要能够根据**步骤配置**中的 `IsStreaming` 标志来正确处理。
*   **显示脚本:** 用于最终渲染 `raw_text` 的显示脚本独立于工作流执行过程。它更像是 Block 或工作流元数据的一部分，由前端在需要渲染时（加载 Block、查看历史）获取并执行。引擎本身不执行最终显示脚本。