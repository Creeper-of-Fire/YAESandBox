# YAESandBox 工作流引擎

## 概述 (Overview)

YAESandBox 工作流引擎是系统核心逻辑的处理单元。它负责根据预定义的、高度可配置的蓝图（工作流配置），处理输入信息，执行一系列复杂的、数据驱动的操作，并最终生成用于改变游戏世界状态的原子化指令和用于呈现的结构化数据。

引擎的核心设计哲学是**声明式、数据驱动、可并行**。它通过分析配置中声明的**变量依赖关系**，自动构建执行图（DAG），并智能地调度任务，以最大限度地利用并行能力。

## 核心概念 (Core Concepts)

*   **工作流 (Workflow):** 一个由多个“祝祷”组成的配置。它不再是简单的线性列表，而是一个**数据流图**的表示，定义了从初始输入到最终输出的完整转换逻辑。
*   **祝祷 (Tuum):** 工作流的基本执行单元，类似于一个“函数”或“节点”。每个祝祷都是符文化、可配置的，封装了一部分具体的处理逻辑（如生成提示词、调用AI、处理文本等）。
*   **符文 (Rune):** 构成祝祷的最小功能单元。每个符文执行一个原子性的任务，并**声明**它需要消费什么**输入变量**（`Consumes`）以及会生产什么**输出变量**（`Produces`）。
*   **变量与作用域 (Variables & Scope):**
    *   **全局变量池:** 整个工作流共享一个全局变量池。这是数据在**祝祷之间**传递的唯一渠道。
    *   **祝祷局部变量池:** 每个祝祷在执行时，都有一个临时的、私有的变量池。符文产生的变量首先进入这里。
    *   **输出映射 (`OutputMappings`):** 祝祷配置的核心。它像一个“出口防火墙”，明确定义了哪些**局部变量**可以被“发布”到**全局变量池**中，以及它们在全局中的名字。任何未被映射的局部变量都会在祝祷执行完毕后被销GL销。
*   **依赖与并行 (Dependencies & Parallelism):**
    *   引擎**不依赖**于祝祷在配置文件中的物理顺序。
    *   它通过分析全局变量的“生产-消费”关系来**自动推断**祝祷间的依赖。
    *   如果祝祷A的输出是祝祷B的输入，则B依赖于A。
    *   没有相互依赖关系的祝祷将被**自动并行执行**。
*   **输出 (Outputs):** 工作流的最终产出是一系列在全局变量池中计算出的值。这些值将由一个特殊的**“终结节点”**（或配置）来决定如何映射到最终的 `WorkflowExecutionResult`，例如哪个变量成为 `RawText`，哪个成为 `Operations`。
    *   **原子化指令列表 (`AtomicOperation[] Operations`):** 一系列描述如何修改 WorldState 或 GameState 的指令。这些指令**不会**被引擎直接执行，而是返回给调用者（`WorkflowService`），最终由 `BlockManager` 根据工作流类型（主/微工作流）和当前 Block 状态决定如何应用。
    *   **结构化原始文本 (`string RawText`):** 工作流产生的**单一文本字符串**。此字符串包含普通的文本内容，并**嵌入了特殊的自定义标签**来标记需要特殊渲染的内容块。这是用于存档、历史记录以及最终前端渲染的基础。
    *   **是否成功 (`isSuccess`):** 工作流是否执行成功。

## `raw_text` 格式详解

`raw_text` 是一个混合了普通文本和自定义标签块的字符串。标签的格式为：`[YAE-BLOCK:scriptId]DATA_CONTENT[/YAE-BLOCK]`

*   `[YAE-BLOCK:scriptId]`: 开始标签，其中 `scriptId` 是一个字符串标识符，**指示前端**应该使用哪个**渲染脚本或组件**来处理 `DATA_CONTENT`。
*   `DATA_CONTENT`: 标签包裹的数据内容，其本身是一个**纯文本字符串**。这个字符串的**内部格式**（例如，普通文本、`|` 分隔列表、JSON 字符串等）由对应的 `scriptId` 的**约定**决定。
*   `[/YAE-BLOCK]`: 结束标签。

**示例 `raw_text`:**
```
这是故事的第一段。
[YAE-BLOCK:char-quote]{"char": "老王", "text": "今天天气不错！"}[/YAE-BLOCK]
请选择：
[YAE-BLOCK:options]吃饭|喝水|睡觉[/YAE-BLOCK]
```

## 工作流执行流程 (Workflow Execution Flow)

1.  **触发:** 外部服务（如 `WorkflowService`）选择一个工作流配置并启动引擎实例，传入初始的触发参数。
2.  **编译 (Compile):**
    *   引擎首先对工作流配置进行**依赖分析**。
    *   它检查每个祝祷的`GlobalConsumers`（来自符文的`Consumes`）和`GlobalProducers`（来自祝祷的`OutputMappings.Keys`）。
    *   基于此构建一个有向无环图（DAG），确定所有祝祷间的依赖关系。
    *   这个阶段会检测出无法执行的**循环依赖**。
3.  **调度与执行 (Schedule & Execute):**
    *   引擎根据编译好的DAG，采用拓扑排序的方式进行调度。
    *   **并行执行批次:** 将所有无未满足依赖的祝祷作为一个“批次”，使用 `Task.WhenAll` 并行执行。
    *   **祝祷执行:** 每个祝祷执行时，会先从全局变量池拉取所需输入到其局部变量池，然后按顺序执行其内部符文。符文间通过局部变量池传递数据。
    *   **结果发布:** 祝祷执行完毕后，根据其`OutputMappings`将结果以线程安全的方式写回全局变量池。
    *   **迭代:** 当一个批次执行完毕后，引擎会检查哪些新的祝祷因为依赖得到满足而变得可执行，并将它们作为下一个并行批次，如此循环直至所有祝祷完成。
4.  **完成:** 所有祝祷执行完毕。引擎将最终的全局变量池返回给调用者，或者根据“终结节点”的配置组装成 `WorkflowExecutionResult`。
5.  **清理:** 引擎实例销毁，所有运行时上下文（包括全局变量池）被丢弃。

## 祝祷详解 (Tuum Details)

一个“祝祷 (Tuum)”是工作流的基本构建块和执行单元。它代表工作流中的一个处理阶段。祝祷是符文化的，可以独立配置和存储，并被不同的工作流引用。

祝祷的核心是围绕一个**主处理脚本 (Main Processing Script)** 来组织的，并根据是否涉及 AI 交互，其执行流程略有不同。

**涉及 AI 交互的祝祷:**

1.  **提示词准备 (Prompt Preparation):**
    *   此阶段负责构建发送给 AI 的提示词 (`prompt_text`)。
    *   它由一系列可配置的“提示词符文”和“脚本符文”组成。
        *   **提示词符文:** 提供文本片段，支持 `{{variable_name}}` 模板。
        *   **脚本符文:** **(轻量级)** 主要用于准备提示词所需的数据，可以读写内部变量，查询 WS/GS (只读)，但不推荐在此生成核心指令。
    *   引擎按顺序执行这些符文，组装出最终的 `prompt_text`。

2.  **AI 请求与流式处理 (AI Request & Stream Processing):**
    *   **AI 调用协调 (由 C# 负责):** C# 后端接收 `prompt_text`，请求调用外部 AI 服务。
    *   **流处理回调脚本 (Stream Processing Callback Script - 可选且独立):**
        *   **仅当 AI 服务以流式方式返回响应时，** C# 后端在接收到**新的文本块 (`chunk`)** 时，会调用为此祝祷配置的**这个特定脚本**。
        *   **输入:** `tuum_execution_time`, `ai_stream_output_added`, `ai_stream_output_total`, 内部变量 (可读写)。
        *   **职责:** 主要负责**生成并请求发送**用于实时前端更新的 `DisplayUpdateDto` (含 `Content`, `StreamingStatus=Streaming`)。应避免复杂逻辑。

3.  **主处理脚本执行 (Main Processing Script Execution):**
    *   **触发时机:** 在 AI 调用**完全结束后** (无论是否流式，成功或失败)，C# 后端会调用祝祷配置的**主处理脚本**。
    *   **输入:**
        *   工作流内部变量 (可读写)。
        *   `tuum_execution_time`: 整个祝祷（包括 AI 等待）的总耗时。
        *   `ai_final_output`: AI 的最终完整输出（流式则为 `ai_stream_output_total` 的最终值）。
    *   **职责 (核心):**
        *   解析 `ai_final_output`。
        *   执行核心业务逻辑。
        *   修改内部变量。
        *   **生成本祝祷主要的 `AtomicOperation`** 并添加到工作流暂存区。
        *   (可选) 请求发送最终的 `DisplayUpdateDto`。

**纯脚本祝祷 (No AI Interaction):**

此类祝祷**直接跳过**上述的“提示词准备”和“AI 请求与流式处理”阶段。

1.  **主处理脚本执行 (Main Processing Script Execution):**
    *   **触发时机:** 祝祷开始执行时，直接调用其配置的**主处理脚本**。
    *   **输入:**
        *   工作流内部变量 (可读写)。
        *   `tuum_execution_time`: 脚本执行耗时。
        *   `ai_final_output`: 通常为空或 null。
    *   **职责 (核心):**
        *   执行核心业务逻辑。
        *   修改内部变量。
        *   **生成本祝祷主要的 `AtomicOperation`** 并添加到工作流暂存区。
        *   (可选) 请求发送 `DisplayUpdateDto`。

**关键简化点:**

*   **核心在主处理脚本:** 无论是 AI 祝祷还是纯脚本祝祷，最终的核心逻辑和指令生成都集中在“主处理脚本”中。纯脚本祝祷可以看作是 `ai_final_output` 为空的 AI 祝祷的特例。
*   **准备阶段简化:** 提示词准备阶段的脚本符文应聚焦于数据准备，而非核心逻辑。
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
*   **符文化:** 工作流由可复用的祝祷组成，易于配置和维护。
*   **灵活性:** 不对工作流的具体实现方式（如必须有“核心文本”变量）做过多硬性规定，允许用户自由组合祝祷和变量。
*   **声明优于指令:** 用户只需声明“做什么”（符文和变量关系），而无需关心“怎么做”（执行顺序和并行）。
*   **数据驱动:** 数据的流动（变量的生产与消费）决定了整个工作流的执行逻辑。
*   **高度并行化:** 架构原生支持并行，以最大化执行效率，尤其适合与耗时的外部AI服务交互。
*   **强作用域控制:** 通过输入/输出映射，严格控制变量的可见性，避免全局污染，使工作流更健壮、更易于维护。

# YAESandBox 工作流引擎接口与配置

## 引擎执行时输入 (Engine Execution Inputs)

当 `WorkflowService` 触发一个工作流执行时，需要向引擎提供以下输入：

1.  **工作流配置 (Workflow Configuration):**
    *   标识要执行的工作流。
    *   包含按顺序执行的祝祷 (Tuum) 列表/引用。
    *   (可能包含) 工作流级别的元数据或设置。

2.  **触发参数 (Trigger Parameters):**
    *   一个字典 (`Dictionary<string, string>`)，包含从前端或其他触发源传递过来的初始参数。
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
        *   `Content`: (string) 要显示的内容 (文本、HTML 片段、JSON 等)。它可以看作是一个临时的 `raw_text` ，使用相同的格式约定，并且被前端 **等同处理** 。
        *   `UpdateMode`: (UpdateMode Enum - `FullSnapshot` 或 `Incremental`) 内容是替换还是增量。
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

## 工作流/祝祷配置 (Workflow/Tuum Configuration)

除了执行时的输入输出，工作流和祝祷本身也需要配置：

1.  **工作流配置:**
    *   **ID/名称:** 唯一标识符。
    *   **祝祷列表:** 按顺序引用的祝祷配置 ID/名称。
    *   **声明的输入参数:** (文档性质) 描述此工作流期望接收哪些触发参数。

2.  **祝祷配置:**
    *   **ID/名称:** 唯一标识符。
    *   **提示词准备符文列表 (可选):** 定义如何构建 AI 提示。
    *   **AI 服务标识 (可选):** 指定此祝祷需要调用哪个外部 AI 服务（如果需要）。
    *   **流式处理回调脚本配置 (可选):** 如果涉及流式 AI，配置用于处理流块的脚本。
    *   **主处理脚本配置:** 配置核心逻辑处理脚本。
    *   **是否流式 (`IsStreaming`, 内部标识):** 祝祷配置需要知道其涉及的 AI 调用是否是流式的，以便引擎和 C# 协调器知道是否需要处理流式回调。

## 其他说明 (Other Notes)

*   **流式行为:** 一个工作流中可以混合包含流式和非流式祝祷。引擎和 C# 协调器需要能够根据**祝祷配置**中的 `IsStreaming` 标志来正确处理。
*   **显示脚本:** 用于最终渲染 `raw_text` 的显示脚本独立于工作流执行过程。它更像是 Block 或工作流元数据的一部分，由前端在需要渲染时（加载 Block、查看历史）获取并执行。引擎本身不执行最终显示脚本。