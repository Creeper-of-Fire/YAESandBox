// --- File: YAESandBox.Workflow/Common/WorkflowExecutionResult.cs ---
using System.Collections.Generic;
using YAESandBox.Core.Action; // 引用包含 AtomicOperation 的命名空间

namespace YAESandBox.Workflow.Common;

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