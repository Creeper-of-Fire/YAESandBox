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
    1.  验证 `parentBlockId` 是否存在且处于 `Idle` 状态。
    2.  创建一个新的子 Block，状态为 `Loading`。
    3.  向所有客户端广播 `ReceiveBlockStatusUpdate` 通知新 Block 的创建和状态。
    4.  在后台异步启动指定 `workflowName` 的执行逻辑。
    5.  工作流执行期间，可能会通过 `ReceiveDisplayUpdate` 推送流式内容。
    6.  工作流执行完毕后，会处理结果，更新 Block 状态 (-> `Idle`, `Error`, 或 `ResolvingConflict`)，并广播最终的 `ReceiveBlockStatusUpdate`、`ReceiveDisplayUpdate` (含 `StreamStatus.Complete` 或 `Error`)，以及可能的 `ReceiveStateUpdateSignal` 或 `ReceiveConflictDetected`。

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
    1.  **不**创建新的 Block 或改变现有 Block 的状态。
    2.  在后台异步启动指定 `workflowName` 的执行逻辑。
    3.  工作流执行期间和完成后，通过 `ReceiveDisplayUpdate` 推送内容，**并将 `targetElementId` 设置为请求中的值**。
    4.  通过 `ReceiveDisplayUpdate` 的 `StreamStatus` 字段告知前端流的开始、进行中和结束 (`Complete` 或 `Error`)。

### 2.3 `ResolveConflict`

提交用户对先前检测到的工作流冲突的解决方案。

*   **方法名:** `ResolveConflict`
*   **参数:** `ResolveConflictRequestDto`
    *   `requestId` (string, Required): 必须与导致冲突的原始 `TriggerMainWorkflow` 请求及收到的 `ConflictDetectedDto` 中的 `requestId` 匹配。
    *   `blockId` (string, Required): 发生冲突的 Block ID。
    *   `resolvedCommands` (List<`AtomicOperationRequestDto`>, Required): 用户确认或修改后的最终原子操作列表。
*   **服务器行为:**
    1.  验证 `blockId` 是否存在且处于 `ResolvingConflict` 状态。
    2.  应用 `resolvedCommands` 到 Block 的 `wsInput` (创建一个临时的 `wsPostAI`)。
    3.  如果应用成功，将 Block 状态更新为 `Idle`，创建 `wsPostUser`。
    4.  如果应用失败，将 Block 状态更新为 `Error`。
    5.  向所有客户端广播最终的 `ReceiveBlockStatusUpdate` (`Idle` 或 `Error`)。
    6.  向所有客户端广播最终的 `ReceiveDisplayUpdate` (包含最终内容, `StreamStatus.Complete` 或 `Error`, `TargetElementId=null`)。
    7.  如果状态有变更，可能广播 `ReceiveStateUpdateSignal`。

## 3. 从服务器调用客户端 (Client Methods)

服务器会调用客户端注册的以下方法来推送更新和通知。

### 3.1 `ReceiveBlockStatusUpdate`

通知客户端某个 Block 的状态码发生了变化。

*   **方法名:** `ReceiveBlockStatusUpdate`
*   **参数:** `BlockStatusUpdateDto`
    *   `blockId` (string): 状态发生变化的 Block ID。
    *   `statusCode` (`BlockStatusCode`): 新的状态码 (`Idle`, `Loading`, `ResolvingConflict`, `Error`)。
*   **客户端处理:**
    *   根据 `blockId` 找到对应的 Block 表示。
    *   更新其状态显示（例如，添加加载指示器、显示错误信息、启用/禁用交互）。

### 3.2 `ReceiveDisplayUpdate`

推送由工作流（主或微）生成的内容，用于更新前端显示。**这是最核心和复杂的消息类型。**

*   **方法名:** `ReceiveDisplayUpdate`
*   **参数:** `DisplayUpdateDto`
    *   `requestId` (string): 关联的原始工作流请求 ID。
    *   `contextBlockId` (string): 主要关联的 Block ID。
    *   `content` (string): 显示内容。
    *   `streamingStatus` (`StreamStatus`): 流状态 (`Streaming`, `Complete`, `Error`)。
    *   `updateMode` (`UpdateMode`): 更新模式 (`FullSnapshot`, `Incremental`)。
    *   `targetElementId` (string?): **关键区分点!**
        *   `null` 或空: 主工作流更新，应用于 `contextBlockId` 的主显示区。
        *   非 `null`: 微工作流更新，应用于 ID 匹配的特定 UI 元素。
    *   `scriptId` (string?, Optional): 建议用于渲染 `content` 的脚本 ID。
    *   `incrementalType` (string?, Optional): 如果 `updateMode` 是 `Incremental`，指定增量类型。
    *   `sequenceNumber` (long?, Optional): 消息序列号，用于排序/去重。
*   **客户端处理:**
    1.  **根据 `targetElementId` 判断目标区域:**
        *   如果为 `null`，找到与 `contextBlockId` 关联的主显示区域。
        *   如果不为 `null`，找到 ID 为 `targetElementId` 的特定 UI 元素。
    2.  **处理 `content`:**
        *   根据 `updateMode` (和 `incrementalType`) 更新目标区域的内容（替换、追加、应用补丁等）。
        *   如果提供了 `scriptId`，可能需要调用相应的渲染逻辑。
    3.  **处理 `streamingStatus`:**
        *   `Streaming`: 更新内容，并预期后续还会有此 `requestId` (和 `targetElementId`) 的消息。可以显示加载/处理中指示器。
        *   `Complete`: 更新内容，这是此 `requestId` (和 `targetElementId`) 的最后一条成功消息。移除加载指示器。
        *   `Error`: 更新内容（可能包含错误信息），这是此 `requestId` (和 `targetElementId`) 的最后一条消息，表示工作流失败。显示错误状态。
    4.  (可选) 使用 `sequenceNumber` 处理乱序或重复消息。

### 3.3 `ReceiveConflictDetected`

当主工作流完成后检测到指令冲突时发送。

*   **方法名:** `ReceiveConflictDetected`
*   **参数:** `ConflictDetectedDto`
    *   `blockId` (string): 发生冲突的 Block ID。
    *   `requestId` (string): 关联的原始工作流请求 ID。
    *   `aiCommands` (List<`AtomicOperationRequestDto`>): AI 生成的完整指令列表。
    *   `userCommands` (List<`AtomicOperationRequestDto`>): 用户提交的完整指令列表（可能已重命名）。
    *   `conflictingAiCommands` (List<`AtomicOperationRequestDto`>): 导致冲突的 AI 指令子集。
    *   `conflictingUserCommands` (List<`AtomicOperationRequestDto`>): 导致冲突的用户指令子集。
*   **客户端处理:**
    *   找到 `blockId` 对应的 Block。
    *   向用户展示冲突信息，通常包括 `aiCommands`、`userCommands` 以及高亮显示 `conflictingAiCommands` 和 `conflictingUserCommands`。
    *   提供界面让用户编辑或选择最终要应用的指令列表。
    *   用户确认后，调用服务器的 `ResolveConflict` 方法，并附带 `requestId`, `blockId` 和最终的指令列表。

### 3.4 `ReceiveStateUpdateSignal`

一个轻量级信号，表明指定 Block 的内部状态（WorldState 或 GameState）可能已发生变化。

*   **方法名:** `ReceiveStateUpdateSignal`
*   **参数:** `StateUpdateSignalDto`
    *   `blockId` (string): 状态可能变化的 Block ID。
    *   `changedEntityIds` (List<string>?): (可选) 如果变化由原子操作引起，包含受影响实体的 ID 列表。
*   **客户端处理:**
    *   这是一个提示，客户端可以选择性地忽略它，或者：
    *   标记与 `blockId` 相关的数据为“可能过时”。
    *   如果需要最新的数据，可以调用相应的 API (例如 `/api/entities?blockId=...`, `/api/entities/{type}/{id}?blockId=...`, `/api/blocks/{blockId}/gamestate`) 来刷新数据。
    *   如果提供了 `changedEntityIds`，可以更精确地只刷新这些实体的数据。

## 4. 相关 DTO 定义

(请参考提供的 C# DTO 文件注释，这里不再重复列出所有字段)

*   `TriggerMainWorkflowRequestDto`
*   `TriggerMicroWorkflowRequestDto`
*   `ResolveConflictRequestDto`
*   `BlockStatusUpdateDto`
*   `DisplayUpdateDto`
*   `ConflictDetectedDto`
*   `StateUpdateSignalDto`
*   `AtomicOperationRequestDto` (在 `ResolveConflictRequestDto` 和 `ConflictDetectedDto` 中使用)
