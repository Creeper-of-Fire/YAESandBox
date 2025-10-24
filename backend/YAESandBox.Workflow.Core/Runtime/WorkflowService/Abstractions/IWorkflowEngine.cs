// --- File: YAESandBox.Workflow/Abstractions/IWorkflowEngine.cs ---

namespace YAESandBox.Workflow.Core.Runtime.WorkflowService.Abstractions;

/// <summary>
/// 工作流执行的最终结果。
/// </summary>
/// <param name="IsSuccess">指示工作流是否成功完成。</param>
/// <param name="ErrorMessage">如果失败，包含错误信息；成功则为 null。</param>
/// <param name="ErrorCode">（可选）更具体的错误代码或类型。</param>
public record WorkflowExecutionResult(
    bool IsSuccess,
    string? ErrorMessage,
    string? ErrorCode
);
