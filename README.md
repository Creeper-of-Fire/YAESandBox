# AI 驱动的文本 RPG 引擎 (原型)

本项目是一个 AI 驱动文本角色扮演游戏 (RPG) 引擎的原型。其核心理念是利用大型语言模型 (LLM) 动态地构建、演变和叙述游戏世界。

**注意事项**
- YAESandBox 的全称是 `YAESandBox Ain't EraSandBox` 

**项目状态: 原型阶段 (Prototype Stage)**

请注意，本项目目前仍处于早期原型阶段。代码主要关注核心功能的探索与实现，可能包含未完全实现的功能、简化的错误处理（可能导致程序崩溃）以及未来可能重构的部分。

**思想：AI创造一切**
- 本项目希望足够包容AI，类似于一个给AI用的记事本，AI可以随意设置变量（目前只需要输入 `@Command` 文本就行了）。
- 所以它的后端非常的简单，没有复杂的NPC交互和条件判断。

## 核心特性 (当前已实现)

*   **后端 API**: 基于 FastAPI 构建，提供 RESTful 接口。
*   **类型化实体引用**: 使用 `TypedID` (类型内唯一 ID) 在整个系统中安全地引用游戏实体 (Item, Character, Place)。
*   **原子化实体操作**: 实现了 `POST /api/entities` (创建/覆盖), `PATCH /api/entities/{type}/{id}` (修改), `DELETE /api/entities/{type}/{id}` (销毁) 的原子化 API 端点，用于精确控制世界状态。
*   **@Command 兼容层**: 提供了 `@Command` 文本指令的解析 (`processing/parser.py`) 和翻译 (`processing/translator.py`) 功能。翻译层负责将 `@Command` 转换为一系列对原子化 API 的调用，并处理了占位符创建和关系维护等复杂逻辑。之后这个模块会移入 Workflow配置 的脚本中。
*   **核心世界状态管理**: `core/world_state.py` 定义了实体和世界容器，实体逻辑已简化，复杂交互移至翻译层。
*   **(进行中)** **日志与错误处理**: 目前日志输出到控制台，错误处理较为直接（倾向于快速失败）。未来计划改进。

## 架构蓝图 (未来方向)

本项目计划采用前后端分离的 Web 架构：

*   **后端 (Python/FastAPI)**:
    *   托管核心游戏逻辑 (`core` 包) 和 `GameState`。
    *   提供 RESTful API 供前端查询状态。
    *   提供 **WebSocket 服务** 用于实时双向通信。
    *   实现 **WorkflowEngine**: 通过配置文件(计划用可视化GUI配置)统一处理所有需要与 AI 交互的工作流（如叙事生成、 NPC 对话、环境描述等）。WorkflowEngine 负责编排 AI 调用、解析响应、通过翻译层提交状态变更指令。每个工作流可能由多个类似于酒馆预设的AI提示词和对应的脚本构成，组合多个API来实现功能。
    *   集成 `AIService` 与 LLM (如 DeepSeek) 交互。交互会是并行的。
    *   通过 `WebSocketNotifier` 将状态变更实时推送给前端。
*   **前端 (倾向于 Vue.js)**:
    *   构建用户界面 (叙事展示、地图、物品栏、角色面板等)。
    *   通过 REST API 获取初始/详细状态。
    *   通过 WebSocket 触发后端工作流并接收实时更新（包括 AI 生成的流式文本和最终状态变更）。

## 技术栈

*   **后端**: Python 3.x, FastAPI
*   **数据验证/模型**: Pydantic
*   **AI 服务接口**: OpenAI Python library (计划对接 DeepSeek API)
*   **(未来) 前端**: Vue.js (倾向)
*   **(未来) 通信**: REST API + WebSockets
