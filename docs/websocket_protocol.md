# WebSocket 协议文档

## 1. 概述

本文档定义了客户端 (前端 UI) 与服务器 (FastAPI 后端) 之间通过 WebSocket (`/ws` 端点) 进行通信的消息格式和协议。

WebSocket 连接主要用于：

1.  **触发后端工作流**: 特别是涉及与 AI 服务交互的流程。
2.  **接收实时更新**: 服务器主动向客户端推送 AI 生成的流式文本块。
3.  **接收工作流结果**: 服务器告知客户端工作流处理完成及其最终结果。
4.  **接收状态变更信号**: 服务器通知客户端游戏核心状态已发生变化，提示客户端重新获取数据。
5.  **发送/接收错误信息**: 处理通信或处理过程中的错误。
6.  **(可选) 测试连接**: 如 Echo 消息。

所有消息都应采用 **JSON 格式**，并包含一个顶层 `type` 字段来标识消息类型。

## 2. 消息方向

*   **客户端 -> 服务器**: 客户端向服务器发送的消息。
*   **服务器 -> 客户端**: 服务器向客户端发送的消息。

## 3. 客户端 -> 服务器 消息

### 3.1 `trigger_workflow`

*   **用途**: 请求服务器启动一个指定的工作流。这是前端发起 AI 交互或复杂后端操作的主要方式。
*   **必需字段**:
    *   `type` (string): 固定为 `"trigger_workflow"`。
    *   `request_id` (string): 客户端生成的唯一 ID，用于将后续的服务器响应（`workflow_update`, `workflow_complete`, `workflow_error`）与此请求关联起来。
    *   `workflow_name` (string): 要触发的工作流的名称 (例如 `"process_user_input"`, `"generate_npc_dialogue"`)。具体可用的名称由后端定义。
*   **可选字段**:
    *   `params` (object): 一个包含工作流所需参数的 JSON 对象。具体内容取决于 `workflow_name`。例如，对于处理用户输入的流程，可能包含 `{"userInput": "向北走"}`。
*   **示例**:
    ```json
    {
      "type": "trigger_workflow",
      "request_id": "client-req-12345",
      "workflow_name": "process_user_input",
      "params": {
        "userInput": "检查我的背包"
      }
    }
    ```

### 3.2 `echo` (可选)

*   **用途**: 用于测试 WebSocket 连接是否正常。服务器会原样返回数据。
*   **必需字段**:
    *   `type` (string): 固定为 `"echo"`。
*   **可选字段**:
    *   `data` (any): 任何客户端希望服务器回显的数据。
*   **示例**:
    ```json
    {
      "type": "echo",
      "data": { "ping": "hello" }
    }
    ```

## 4. 服务器 -> 客户端 消息

### 4.1 `workflow_update`

*   **用途**: 在工作流处理期间，实时向客户端推送部分结果，通常是 AI 生成的流式文本块。
*   **必需字段**:
    *   `type` (string): 固定为 `"workflow_update"`。
    *   `request_id` (string): 对应触发此工作流的客户端 `trigger_workflow` 消息中的 `request_id`。
    *   `data` (object): 包含更新数据的对象。目前定义的类型如下：
        *   `type` (string): `"stream_chunk"` - 表示这是一个文本流块。
        *   `content` (string): AI 生成的文本块。
*   **示例 (流式文本块)**:
    ```json
    {
      "type": "workflow_update",
      "request_id": "client-req-12345",
      "data": {
        "type": "stream_chunk",
        "content": "你打开背包，看到里面有"
      }
    }
    ```
    ```json
    {
      "type": "workflow_update",
      "request_id": "client-req-12345",
      "data": {
        "type": "stream_chunk",
        "content": "一个苹果和一把生锈的钥匙。"
      }
    }
    ```

### 4.2 `workflow_complete`

*   **用途**: 标志着由 `request_id` 标识的工作流已处理完毕，并提供最终结果。
*   **必需字段**:
    *   `type` (string): 固定为 `"workflow_complete"`。
    *   `request_id` (string): 对应触发此工作流的客户端 `trigger_workflow` 消息中的 `request_id`。
    *   `result` (object): 包含工作流最终结果的对象。
        *   `full_text` (string): AI 返回的完整拼接文本。
        *   `execution_status` (string): 指令执行的总结状态。可能的值：
            *   `"success"`: 所有解析出的指令都成功执行。
            *   `"partial_failure"`: 部分指令执行成功，部分失败。
            *   `"failure"`: 所有指令执行失败，或执行过程中发生严重错误。
            *   `"no_commands"`: AI 响应中未解析出任何指令。
        *   `execution_error_message` (string | null): **(新增)** 一个对用户友好的、关于指令执行失败的提示信息。**在当前版本中，此字段始终为 `null`。** 未来版本可能会根据 `execution_status` 提供具体信息（例如 "无法移动到那里"、"背包已满" 等）。
*   **示例**:
    ```json
    {
      "type": "workflow_complete",
      "request_id": "client-req-12345",
      "result": {
        "full_text": "你打开背包，看到里面有一个苹果和一把生锈的钥匙。@Command: Character:player set_attribute status='查看背包'",
        "execution_status": "success",
        "execution_error_message": null
      }
    }
    ```
    ```json
    {
      "type": "workflow_complete",
      "request_id": "client-req-67890",
      "result": {
        "full_text": "你尝试向北走，但撞到了一堵墙。@Command: Place:current_room add_log '撞墙了'",
        "execution_status": "success", // 假设 add_log 命令成功
        "execution_error_message": null // 即使玩家没移动成功，命令执行是成功的，所以这里是 null
                                        // 叙事文本 full_text 描述了实际情况
      }
    }
    ```
    ```json
    {
      "type": "workflow_complete",
      "request_id": "client-req-11223",
      "result": {
        "full_text": "你感觉什么都没发生。@Command: Item:non_existent_sword modify_attribute owner=Player:player",
        "execution_status": "failure", // 指令执行失败 (物品不存在)
        "execution_error_message": null // 当前版本固定为 null
      }
    }
    ```

### 4.3 `state_update_signal` (新增)

*   **用途**: 当游戏核心状态（如实体属性、位置关系等）通过原子 API (`POST/PATCH/DELETE /api/entities/...`) 发生变更时，服务器向 **所有** 连接的客户端广播此消息。客户端收到此信号后，应主动调用相关的 RESTful API (例如 `GET /api/game/view` 或 `GET /entities/...`) 来获取最新的游戏状态以更新 UI。
*   **必需字段**:
    *   `type` (string): 固定为 `"state_update_signal"`。
    *   `payload` (object): 一个空对象 `{}`。此消息仅作为信号，不包含具体的变更内容。
*   **示例**:
    ```json
    {
      "type": "state_update_signal",
      "payload": {}
    }
    ```

### 4.4 `error`

*   **用途**: 服务器向客户端发送一般性错误信息，通常是由于无效的客户端请求或服务器内部问题导致无法正常处理某个请求（但不是工作流本身的失败）。
*   **必需字段**:
    *   `type` (string): 固定为 `"error"`。
    *   `message` (string): 对用户友好的错误描述。
*   **可选字段**:
    *   `request_id` (string | null): 如果错误与某个特定的客户端请求相关联，则包含对应的 `request_id`。如果错误是全局的或无法关联，则为 `null`。
*   **示例**:
    ```json
    {
      "type": "error",
      "message": "无效的 JSON 格式。",
      "request_id": null
    }
    ```
    ```json
    {
      "type": "error",
      "message": "未知消息类型: trigger_wordflow", // 注意拼写错误
      "request_id": "client-req-badtype"
    }
    ```

### 4.5 `workflow_error`

*   **用途**: 当一个特定的工作流在处理过程中遇到严重、意外的内部错误（例如 AI 服务调用失败、内部逻辑崩溃）时，服务器发送此消息。这与 `workflow_complete` 中的 `execution_status` 不同，后者表示指令执行的结果，而 `workflow_error` 表示工作流本身无法完成。
*   **必需字段**:
    *   `type` (string): 固定为 `"workflow_error"`。
    *   `request_id` (string): 对应触发此工作流的客户端 `trigger_workflow` 消息中的 `request_id`。
    *   `error` (string): 对用户友好的错误描述，说明工作流处理失败。应避免暴露过多内部细节。
*   **示例**:
    ```json
    {
      "type": "workflow_error",
      "request_id": "client-req-ai-fail",
      "error": "处理您的请求时发生内部错误，请稍后再试。"
    }
    ```

### 4.6 `echo_response` (可选)

*   **用途**: 对客户端 `echo` 请求的响应。
*   **必需字段**:
    *   `type` (string): 固定为 `"echo_response"`。
*   **可选字段**:
    *   `original_data` (any): 客户端在 `echo` 请求中发送的 `data` 字段内容。
*   **示例**:
    ```json
    {
      "type": "echo_response",
      "original_data": { "ping": "hello" }
    }
    ```