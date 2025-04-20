# YAESandBox SignalR (GameHub) 通信文档

本文档描述了 YAESandBox 后端通过 SignalR `GameHub` 与客户端进行实时通信的协议。

## 1. 连接

客户端应连接到服务器上的 `/gamehub` 端点。

## 2. 从客户端调用服务器 (Hub Methods)

客户端可以通过 SignalR 连接调用以下服务器方法来触发后端操作。

### 2.1 `TriggerMainWorkflow`

触发一个**主工作流**。主工作流通常涉及创建一个新的子 Block 来代表故事或游戏状态的进展，并可能伴随异步的内容生成和状态更新。

*   **方法名:** `TriggerMainWorkflow`
*   **参数:** `TriggerMainWorkflowRequestDto`
    *   `requestId` (string, Required): 客户端生成的唯一 ID，用于追踪整个工作流生命周期。
    *   `parentBlockId` (string, Required): 在哪个 Block 下创建新的子 Block。
    *   `workflowName` (string, Required): 要执行的工作流的名称。
    *   `params` (Dictionary<string, object?>, Optional): 传递给工作流的参数。
*   **服务器行为:**
    1.  验证 `parentBlockId` 是否存在且状态允许创建子节点 (通常是 `Idle`)。
    2.  创建一个新的子 Block，初始状态为 `Loading`。
    3.  向所有客户端广播 `ReceiveBlockStatusUpdate` 通知新 Block 的创建和其 `Loading` 状态。
    4.  在后台异步启动指定 `workflowName` 的执行逻辑。
    5.  工作流执行期间，可能会通过 `ReceiveDisplayUpdate` (带有 `Streaming` 状态和 `targetElementId=null`) 推送流式内容给所有客户端。
    6.  工作流执行完毕后，会处理结果（包括文本、生成的原子操作等），并根据结果更新 Block 状态 (变为 `Idle`, `Error`, 或 `ResolvingConflict`)。
    7.  服务器随后会广播：
        *   最终的 `ReceiveBlockStatusUpdate` (通知新的状态 `Idle`, `Error`, 或 `ResolvingConflict`)。
        *   最终的 `ReceiveDisplayUpdate` (包含最终内容, `StreamStatus.Complete` 或 `Error`, `TargetElementId=null`)。
        *   如果检测到冲突，广播 `ReceiveConflictDetected`。
        *   如果 Block 的内部状态 (一般为WorldState) 因应用原子操作而改变，广播 `ReceiveBlockUpdateSignal`。

### 2.2 `TriggerMicroWorkflow`

触发一个**微工作流**。微工作流通常用于执行不直接改变核心叙事状态（即不创建新 Block）的辅助任务，例如生成 UI 建议、获取信息等。其结果主要用于更新特定的 UI 元素。

*   **方法名:** `TriggerMicroWorkflow`
*   **参数:** `TriggerMicroWorkflowRequestDto`
    *   `requestId` (string, Required): 客户端生成的唯一 ID。
    *   `contextBlockId` (string, Required): 触发微工作流时用户界面的上下文 Block ID。
    *   `targetElementId` (string, Required): (关键!) 目标 UI 元素的 ID，用于前端识别更新区域。
    *   `workflowName` (string, Required): 要执行的微工作流的名称。
    *   `params` (Dictionary<string, object?>, Optional): 传递给微工作流的参数。
*   **服务器行为:**
    1.  **不**创建新的 Block 或改变现有 Block 的状态码。
    2.  在后台异步启动指定 `workflowName` 的执行逻辑。
    3.  工作流执行期间和完成后，通过 `ReceiveDisplayUpdate` 推送内容给所有客户端，**并将 `targetElementId` 设置为请求中的值**。
    4.  通过 `ReceiveDisplayUpdate` 的 `StreamStatus` 字段告知前端流的开始 (`Streaming`)、进行中 (`Streaming`) 和结束 (`Complete` 或 `Error`)。微工作流通常不直接触发 `ReceiveBlockStatusUpdate` 或 `ReceiveBlockUpdateSignal`。

### 2.3 `RegenerateBlock`

请求重新生成一个**现有的 Block**。这会触发一个类似主工作流的过程，但作用于已存在的 Block，而不是创建新的子 Block。

*   **方法名:** `RegenerateBlock`
*   **参数:** `RegenerateBlockRequestDto`
    *   `requestId` (string, Required): 客户端生成的唯一 ID。
    *   `blockId` (string, Required): 要重新生成的 Block 的 ID。
    *   `workflowName` (string, Required): 用于重新生成的工作流名称。
    *   `params` (Dictionary<string, object?>, Optional): 传递给工作流的参数。
*   **服务器行为:**
    1.  验证 `blockId` 是否存在且状态允许重新生成 (通常是 `Idle` 或 `Error`)。
    2.  将目标 Block 的状态设置为 `Loading`。
    3.  向所有客户端广播 `ReceiveBlockStatusUpdate` 通知该 Block 进入 `Loading` 状态。
    4.  在后台异步启动指定 `workflowName` 的执行逻辑（与 `TriggerMainWorkflow` 类似，但作用于现有 Block）。
    5.  工作流执行期间，可能会通过 `ReceiveDisplayUpdate` (带有 `Streaming` 状态和 `targetElementId=null`) 推送流式内容。
    6.  工作流执行完毕后，处理结果并更新 Block 状态 (变为 `Idle`, `Error`, 或 `ResolvingConflict`)。
    7.  服务器随后会广播：
        *   最终的 `ReceiveBlockStatusUpdate` (通知新的状态 `Idle`, `Error`, 或 `ResolvingConflict`)。
        *   最终的 `ReceiveDisplayUpdate` (包含最终内容, `StreamStatus.Complete` 或 `Error`, `TargetElementId=null`)。
        *   如果检测到冲突，广播 `ReceiveConflictDetected`。
        *   如果 Block 的内部状态 (一般为WorldState) 因应用原子操作而改变，广播 `ReceiveBlockUpdateSignal`。

### 2.4 `ResolveConflict`

提交用户对先前检测到的工作流冲突的解决方案。

*   **方法名:** `ResolveConflict`
*   **参数:** `ResolveConflictRequestDto`
    *   `requestId` (string, Required): 必须与导致冲突的原始工作流请求及收到的 `ConflictDetectedDto` 中的 `requestId` 匹配。
    *   `blockId` (string, Required): 发生冲突的 Block ID。
    *   `resolvedCommands` (List<`AtomicOperationRequestDto`>, Required): 用户确认或修改后的最终原子操作列表。
*   **服务器行为:**
    1.  验证 `blockId` 是否存在且处于 `ResolvingConflict` 状态。
    2.  尝试应用 `resolvedCommands` 到 Block 的状态中。
    3.  根据应用结果，将 Block 状态更新为 `Idle` (成功) 或 `Error` (失败)。
    4.  服务器随后会广播：
        *   最终的 `ReceiveBlockStatusUpdate` (通知状态变为 `Idle` 或 `Error`)。
        *   (注意：当前实现可能不会在此处发送特定的 `ReceiveDisplayUpdate`，客户端主要依赖状态更新)。
        *   如果 Block 的内部状态 (一般为WorldState) 因应用 `resolvedCommands` 而改变，广播 `ReceiveBlockUpdateSignal`。

## 3. 从服务器调用客户端 (Client Methods)

服务器会调用客户端注册的以下方法来推送更新和通知。**所有这些通知都是全局广播的**，客户端需要自行判断是否关心以及如何处理收到的消息。

### 3.1 工作流与 Block 生命周期通知

这些通知直接关联到 Block 的创建、工作流（主流程、重新生成、微流程）的执行过程和结果，以及冲突的检测与解决。

#### 3.1.1 `ReceiveBlockStatusUpdate`

通知客户端某个 Block 的**状态码**发生了变化。这是追踪 Block 生命周期的关键信号。

*   **触发时机:**
    *   通过 `TriggerMainWorkflow` 创建新 Block 时 (-> `Loading`)。
    *   通过 `RegenerateBlock` 开始重新生成时 (-> `Loading`)。
    *   主工作流或重新生成流程执行完成后 (-> `Idle`, `Error`, 或 `ResolvingConflict`)。
    *   通过 `ResolveConflict` 解决冲突后 (-> `Idle` 或 `Error`)。
    *   通过 API 手动删除 Block 时 (-> `Deleted`，虽然前端通常通过拓扑更新感知删除)。
*   **方法名:** `ReceiveBlockStatusUpdate`
*   **参数:** `BlockStatusUpdateDto`
    *   `blockId` (string): 状态发生变化的 Block ID。
    *   `statusCode` (`BlockStatusCode`): 新的状态码 (`Loading`, `Idle`, `ResolvingConflict`, `Error`, `Deleted`)。
*   **客户端处理:**
    *   根据 `blockId` 找到对应的 Block 表示。
    *   更新其视觉状态（例如，显示加载指示器、错误标记、冲突待解决状态、或标记为正常可交互）。
    *   根据状态启用/禁用相关操作（如编辑、触发新工作流等）。

#### 3.1.2 `ReceiveDisplayUpdate`

推送由工作流（主流程、重新生成、微流程）生成的内容，用于更新前端显示。**这是内容展示的核心消息。**

*   **触发时机:**
    *   主工作流或重新生成流程执行期间 (发送流式内容，`StreamingStatus = Streaming`, `TargetElementId = null`)。
    *   主工作流或重新生成流程执行完成时 (发送最终内容/结果，`StreamingStatus = Complete` 或 `Error`, `TargetElementId = null`)。
    *   微工作流执行期间和完成时 (发送流式或最终内容，`StreamingStatus = Streaming`, `Complete`, 或 `Error`, **`TargetElementId` 被设置为请求时的值**)。
*   **方法名:** `ReceiveDisplayUpdate`
*   **参数:** `DisplayUpdateDto` (关键字段如下)
    *   `requestId` (string): 关联的原始工作流请求 ID。
    *   `contextBlockId` (string): 主要关联的 Block ID。
    *   `content` (string): 显示内容。
    *   `streamingStatus` (`StreamStatus`): 流状态 (`Streaming`, `Complete`, `Error`)。
    *   `targetElementId` (string?): **关键区分点!**
        *   `null` 或空: 更新 `contextBlockId` 的主显示区。
        *   非 `null`: 更新 ID 匹配的特定 UI 元素。
    *   (其他字段如 `updateMode`, `scriptId`, `incrementalType`, `sequenceNumber` 提供更精细控制)。
*   **客户端处理:**
    1.  **根据 `targetElementId` 确定目标区域。**
    2.  **根据 `updateMode` 和 `streamingStatus` 更新内容。**
    3.  **根据 `streamingStatus` 管理加载/完成/错误状态的显示。**
    4.  (可选) 处理 `scriptId`, `incrementalType`, `sequenceNumber`。

#### 3.1.3 `ReceiveConflictDetected`

当主工作流或重新生成流程完成后，检测到其生成的原子操作与用户在 `Loading` 期间提交的操作存在冲突时发送。

*   **触发时机:** 主工作流或重新生成流程完成，且 `BlockManager` 检测到不可自动解决的冲突。
*   **方法名:** `ReceiveConflictDetected`
*   **参数:** `ConflictDetectedDto`
    *   `blockId` (string): 发生冲突的 Block ID。
    *   `requestId` (string): 关联的原始工作流请求 ID。
    *   `aiCommands` (List<`AtomicOperationRequestDto`>): 工作流生成的指令。
    *   `userCommands` (List<`AtomicOperationRequestDto`>): 用户提交的指令。
    *   `conflictingAiCommands` (List<`AtomicOperationRequestDto`>): 冲突的 AI 指令子集。
    *   `conflictingUserCommands` (List<`AtomicOperationRequestDto`>): 冲突的用户指令子集。
*   **客户端处理:**
    *   找到 `blockId` 对应的 Block。
    *   向用户展示冲突详情，提供解决界面。
    *   用户确认后，调用服务器的 `ResolveConflict` 方法提交解决方案。

### 3.2 状态变更提示通知

这类通知提示客户端某个 Block 的内部数据可能发生了变化，但不直接提供更新后的数据。鼓励客户端在需要时通过 API 获取最新信息。

#### 3.2.1 `ReceiveBlockUpdateSignal`

一个轻量级信号，表明指定 Block 的内部状态（如 WorldState 中的实体属性/关系、 GameState 设置、本 Block 和其他 Block 的拓扑结构、Block的内容）**可能**已发生变化。

*   **触发时机:**
    *   通过 `AtomicController` API 成功执行或暂存原子操作后。
    *   主工作流或重新生成流程成功应用原子操作后 (在 `Idle` 状态转换时)。
    *   通过 `ResolveConflict` 成功应用解决方案中的原子操作后。
    *   通过 `GameStateController` API 成功修改 GameState 后。
    *   通过 `BlocksController` API (如 `PATCH /api/blocks/{blockId}`) 修改 Block 内容或元数据后 (虽然这个信号主要关联内部状态，但修改 Block 本身也可能触发，需要结合 `changedFields` 判断)。
    *   通过 Block 管理 API (如 `POST /api/manage/blocks/{blockId}/move`) 移动 Block 后 (会更新父/子关系，可能触发父、子及自身的信号)。这个API仅供内部使用，外部是不可见的，但是依旧需要处理它带来的可能的更新。
*   **方法名:** `ReceiveBlockUpdateSignal`
*   **参数:** `BlockUpdateSignalDto`
    *   `blockId` (string): 状态可能变化的 Block ID。
    *   `changedFields` (List<`BlockDataFields`>?): (可选) 指示哪些类型的数据可能变化 (如 `WorldState`, `GameState`, `ChildrenInfo`, `ParentBlockId`, `BlockContent`, `Metadata`)。
    *   `changedEntityIds` (List<string>?): (可选) 如果变化由原子操作引起，可能包含受影响实体的 ID 列表。
*   **客户端处理:**
    *   这是一个提示，客户端可以选择性地忽略，或者：
    *   标记与 `blockId` 相关的数据（整个 Block、特定实体、GameState）为“可能过时”。
    *   如果当前用户界面正在显示与该 `blockId` 相关的信息，或者需要最新数据时，**调用相应的 REST API** (例如 `/api/blocks/{blockId}`, `/api/entities?blockId=...`, `/api/entities/{type}/{id}?blockId=...`, `/api/blocks/{blockId}/gamestate`, `/api/blocks/topology`) 来刷新数据。
    *   利用 `changedFields` 和 `changedEntityIds` 可以进行更精细的按需刷新。

## 4. 相关 DTO 定义

(请参考提供的 C# DTO 文件注释，关键 DTOs 包括)

*   `TriggerMainWorkflowRequestDto`
*   `TriggerMicroWorkflowRequestDto`
*   `RegenerateBlockRequestDto`
*   `ResolveConflictRequestDto`
*   `BlockStatusUpdateDto`
*   `DisplayUpdateDto`
*   `ConflictDetectedDto`
*   `BlockUpdateSignalDto`
*   `BlockStatusCode` (Enum)
*   `StreamStatus` (Enum)
*   `UpdateMode` (Enum)
*   `BlockDataFields` (Enum)