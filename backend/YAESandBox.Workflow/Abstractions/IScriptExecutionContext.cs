// --- File: YAESandBox.Workflow/Abstractions/IScriptExecutionContext.cs ---
using System.Collections.Generic;
using System.Threading.Tasks;
using YAESandBox.Core.Action; // For AtomicOperation
using YAESandBox.Workflow.Common; // For DisplayUpdateRequestPayload

namespace YAESandBox.Workflow.Abstractions;

/// <summary>
/// 提供给 C# 脚本的执行上下文接口。
/// 这是脚本与工作流引擎和外部系统交互的桥梁。
/// </summary>
public interface IScriptExecutionContext
{
    /// <summary>
    /// 访问工作流全局变量 (跨步骤共享, dynamic 类型以支持运行时添加属性)。
    /// </summary>
    dynamic Globals { get; }

    /// <summary>
    /// 访问当前步骤的局部变量 (步骤内有效, dynamic 类型)。
    /// </summary>
    dynamic Locals { get; }

    /// <summary>
    /// 管理当前步骤的提示词列表。
    /// </summary>
    IPromptManager Prompts { get; }

    /// <summary>
    /// 获取触发工作流时的只读参数。
    /// </summary>
    IReadOnlyDictionary<string, string> TriggerParams { get; }

     /// <summary>
    /// 获取当前步骤（或流处理回调）接收到的 AI 流式输出块 (如果适用)。
    /// </summary>
    string? AiStreamChunk { get; } // 用于流处理回调脚本

    /// <summary>
    /// 获取当前步骤（或流处理回调）已累积的 AI 流式输出总量 (如果适用)。
    /// </summary>
    string? AiStreamTotal { get; } 

    /// <summary>
    /// 获取整个步骤到目前为止的执行时间（毫秒）。
    /// </summary>
    long StepExecutionTimeMs { get; }

    /// <summary>
    /// 请求发送一个中间的显示更新给前端。
    /// C# 后端会填充其他字段 (RequestId, BlockId等) 并通过 INotifierService 发送。
    /// </summary>
    /// <param name="payload">包含 Content, UpdateMode 等的负载。</param>
    void RequestDisplayUpdate(DisplayUpdateRequestPayload payload);

    /// <summary>
    /// 将一条原子化操作添加到最终的工作流结果列表。
    /// </summary>
    /// <param name="operation">要添加的操作。</param>
    void AddAtomicOperation(AtomicOperation operation);

    /// <summary>
    /// 访问只读数据查询接口。
    /// </summary>
    IWorkflowDataAccess DataAccess { get; }

    /// <summary>
    /// 提供模板渲染功能 (隐式使用 Globals 和 Locals)。
    /// </summary>
    /// <param name="template">包含 {{变量名}} 的模板字符串。</param>
    /// <returns>渲染后的字符串。</returns>
    string RenderTemplate(string template);
}
