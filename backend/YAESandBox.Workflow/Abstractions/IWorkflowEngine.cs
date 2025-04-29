// --- File: YAESandBox.Workflow/Abstractions/IWorkflowEngine.cs ---
using System.Collections.Generic;
using System.Threading.Tasks;
using YAESandBox.Workflow.Common;

namespace YAESandBox.Workflow.Abstractions;

/// <summary>
/// 工作流引擎的核心接口。
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>
    /// 执行指定的工作流。
    /// </summary>
    /// <param name="workflowId">要执行的工作流的 ID。</param>
    /// <param name="triggerParams">触发工作流的参数。</param>
    /// <param name="dataAccess">提供数据访问能力的实例。</param>
    /// <param name="requestDisplayUpdateCallback">用于请求发送 DisplayUpdate 的回调委托。</param>
    /// <param name="cancellationToken">(可选) 用于取消操作。</param>
    /// <returns>包含执行结果 (成功/失败, 操作列表, raw_text) 的对象。</returns>
    Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
        string workflowId,
        IReadOnlyDictionary<string, string> triggerParams,
        IWorkflowDataAccess dataAccess,
        Action<DisplayUpdateRequestPayload> requestDisplayUpdateCallback,
        CancellationToken cancellationToken = default
    );

    // --- 未来可能需要的 AI 流式处理相关回调 ---
    // Func<string, /*stepId*/ string, /*prompt*/ IAsyncEnumerable<string> /*streamChunks*/>? getAiStreamResponseAsyncCallback = null,
    // Func<string, /*stepId*/ string, /*chunk*/ Task>? streamChunkCallback = null, // 引擎内部调用流处理脚本的回调? 或者直接在引擎实现
}