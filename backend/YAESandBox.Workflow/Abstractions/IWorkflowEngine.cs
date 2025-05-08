// --- File: YAESandBox.Workflow/Abstractions/IWorkflowEngine.cs ---

using YAESandBox.API.DTOs.WebSocket;
using YAESandBox.Core.Action;

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
    Task<WorkflowExecutionResult> ExecuteWorkflowAsync(string workflowId,
        IReadOnlyDictionary<string, string> triggerParams,
        IWorkflowDataAccess dataAccess,
        Action<ValueTuple<bool>> requestDisplayUpdateCallback,
        CancellationToken cancellationToken = default // TODO:说实在的，这是AI生成的，我并不知道这玩意有什么用
    );
}

/// <summary>
/// 工作流执行的最终结果。
/// </summary>
/// <param name="IsSuccess">指示工作流是否成功完成。</param>
/// <param name="ErrorMessage">如果失败，包含错误信息；成功则为 null。</param>
/// <param name="ErrorCode">（可选）更具体的错误代码或类型。</param>
/// <param name="Operations">生成的原子化操作列表。</param>
/// <param name="RawText">最终生成的原始文本。</param>
public record WorkflowExecutionResult(
    bool IsSuccess,
    string? ErrorMessage,
    string? ErrorCode, // 可以用枚举或其他方式定义错误类型
    List<AtomicOperation> Operations,
    string RawText
);

/// <summary>
/// 工作流脚本请求发送显示更新时使用的负载。
/// 这是最终 DisplayUpdateDto 的子集，包含脚本能控制的部分。
/// </summary>
/// <param name="Content">要显示的内容 (遵循 raw_text 格式约定)</param>
/// <param name="UpdateMode">内容是替换还是增量</param>
public record DisplayUpdateRequestPayload(
    string Content,
    UpdateMode UpdateMode = UpdateMode.FullSnapshot
);