// --- File: YAESandBox.Workflow/Abstractions/IWorkflowEngine.cs ---

using YAESandBox.Core.Action;
using YAESandBox.Core.DTOs.WebSocket;

namespace YAESandBox.Workflow.Abstractions;

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