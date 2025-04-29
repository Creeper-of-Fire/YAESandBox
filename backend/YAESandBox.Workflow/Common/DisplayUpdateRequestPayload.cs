// --- File: YAESandBox.Workflow/Common/DisplayUpdateRequestPayload.cs ---
using YAESandBox.API.DTOs.WebSocket; // 引用包含 UpdateMode 的命名空间

namespace YAESandBox.Workflow.Common;

/// <summary>
/// 工作流脚本请求发送显示更新时使用的负载。
/// 这是最终 DisplayUpdateDto 的子集，包含脚本能控制的部分。
/// </summary>
/// <param name="Content">要显示的内容 (遵循 raw_text 格式约定)</param>
/// <param name="UpdateMode">内容是替换还是增量</param>
/// <param name="IncrementalType">(可选) 增量更新类型</param>
/// <param name="TargetElementId">(可选) 微工作流的目标 UI 元素 ID</param>
public record DisplayUpdateRequestPayload(
    string Content,
    UpdateMode UpdateMode = UpdateMode.FullSnapshot,
    string? IncrementalType = null,
    string? TargetElementId = null // 微工作流可能需要指定
);