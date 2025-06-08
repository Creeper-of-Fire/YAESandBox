// --- File: YAESandBox.Workflow/Abstractions/IWorkflowEngine.cs ---

namespace YAESandBox.Workflow.Abstractions;

/// <summary>
/// 工作流执行的最终结果。
/// </summary>
/// <param name="IsSuccess">指示工作流是否成功完成。</param>
/// <param name="ErrorMessage">如果失败，包含错误信息；成功则为 null。</param>
/// <param name="ErrorCode">（可选）更具体的错误代码或类型。</param>
// /// <param name="Operations">生成的原子化操作列表。</param>
// /// <param name="RawText">最终生成的原始文本。</param>
public record WorkflowExecutionResult(
    bool IsSuccess,
    string? ErrorMessage,
    string? ErrorCode
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

/// <summary>
/// 指示消息在流式传输过程中的状态。
/// </summary>
public enum StreamStatus
{
    /// <summary>
    /// 工作流仍在处理中，后续可能还会有消息。
    /// </summary>
    Streaming,

    /// <summary>
    /// 工作流已成功完成，这是此 RequestId 的最后一条消息（对于该 TargetElementId，如果是微工作流）。
    /// </summary>
    Complete,

    /// <summary>
    /// 工作流执行过程中发生错误而中止。这通常是此 RequestId 的最后一条消息。
    /// </summary>
    Error
}

/// <summary>
/// 指示消息的更新方式。
/// </summary>
public enum UpdateMode
{
    /// <summary>
    /// 消息包含目标区域的完整内容，应替换现有内容。
    /// </summary>
    FullSnapshot,

    /// <summary>
    /// 消息包含对现有内容的增量更改。
    /// </summary>
    Incremental
}