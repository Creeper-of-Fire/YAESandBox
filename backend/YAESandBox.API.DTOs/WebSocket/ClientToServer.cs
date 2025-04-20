using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend;

namespace YAESandBox.API.DTOs.WebSocket;

// --- WebSocket (SignalR) 消息 DTOs ---

/// <summary>
/// (客户端 -> 服务器)
/// 用于通过 SignalR 触发 **主工作流** 的请求。
/// 主工作流通常会导致创建一个新的子 Block 来表示新的叙事状态。
/// </summary>
public record TriggerMainWorkflowRequestDto
{
    /// <summary>
    /// 客户端生成的唯一请求 ID，用于追踪此工作流调用的整个生命周期，
    /// 包括可能的流式更新和最终结果。
    /// </summary>
    [Required(ErrorMessage = "必须提供请求 ID")]
    public string RequestId { get; set; } = null!;

    /// <summary>
    /// 要在其下创建新子 Block 的父 Block 的 ID。
    /// </summary>
    [Required(ErrorMessage = "必须提供父 Block ID")]
    public string ParentBlockId { get; set; } = null!;

    /// <summary>
    /// 要调用的工作流的名称或标识符。
    /// </summary>
    [Required(ErrorMessage = "必须提供工作流名称")]
    public string WorkflowName { get; set; } = null!;

    /// <summary>
    /// 传递给工作流的参数字典。键值对的具体内容取决于所调用的工作流。
    /// </summary>
    public Dictionary<string, object?> Params { get; set; } = new();
}

/// <summary>
/// (客户端 -> 服务器)
/// 用于通过 SignalR 触发 **微工作流** 的请求。
/// 微工作流通常用于生成辅助信息、建议或执行不直接改变核心叙事状态（即不创建新 Block）的操作。
/// 其结果通常用于更新 UI 的特定部分。
/// </summary>
public record TriggerMicroWorkflowRequestDto
{
    /// <summary>
    /// 客户端生成的唯一请求 ID，用于追踪此工作流调用的整个生命周期。
    /// </summary>
    [Required(ErrorMessage = "必须提供请求 ID")]
    public string RequestId { get; set; } = null!;

    /// <summary>
    /// 触发此微工作流时，用户界面所在的上下文 Block 的 ID。
    /// 工作流逻辑可能会使用此 Block 的状态作为输入。
    /// </summary>
    [Required(ErrorMessage = "必须提供上下文 Block ID")]
    public string ContextBlockId { get; set; } = null!;

    /// <summary>
    /// (关键) 目标 UI 元素或逻辑区域的标识符。
    /// 后端会将此工作流产生的 <see cref="DisplayUpdateDto"/> 消息的 <see cref="DisplayUpdateDto.TargetElementId"/> 设置为此值，
    /// 以便前端知道更新哪个 UI 组件。该 ID 由前端定义和解释。
    /// </summary>
    [Required(ErrorMessage = "必须提供目标元素 ID")]
    public string TargetElementId { get; set; } = null!;

    /// <summary>
    /// 要调用的微工作流的名称或标识符。
    /// </summary>
    [Required(ErrorMessage = "必须提供工作流名称")]
    public string WorkflowName { get; set; } = null!;

    /// <summary>
    /// 传递给微工作流的参数字典。
    /// </summary>
    public Dictionary<string, object?> Params { get; set; } = new();
}

/// <summary>
/// (客户端 -> 服务器)
/// 用于通过 SignalR 请求重新生成现有 Block 的内容和状态。
/// 只有主工作流对此有用。
/// </summary>
public record RegenerateBlockRequestDto
{
    /// <summary>
    /// 唯一的请求 ID，用于追踪。
    /// </summary>
    [Required]
    public string RequestId { get; set; } = null!;

    /// <summary>
    /// 要重新生成的 Block 的 ID。
    /// </summary>
    [Required]
    public string BlockId { get; set; } = null!;

    /// <summary>
    /// 用于重新生成的工作流名称。
    /// </summary>
    [Required]
    public string WorkflowName { get; set; } = null!;

    /// <summary>
    /// 传递给重新生成工作流的参数。
    /// </summary>
    public Dictionary<string, object?> Params { get; set; } = new();
}

/// <summary>
/// (客户端 -> 服务器)
/// 用于通过 SignalR 提交 **冲突解决方案** 的请求。
/// 当主工作流完成后检测到与用户修改冲突时，前端会收到 <see cref="ConflictDetectedDto"/>。
/// 用户解决冲突后，通过此 DTO 将最终确定的原子操作列表提交回后端。
/// </summary>
public record ResolveConflictRequestDto
{
    /// <summary>
    /// 必须与导致冲突的原始工作流请求 (<see cref="TriggerMainWorkflowRequestDto"/>) 的 RequestId 相同，
    /// 也应与收到的 <see cref="ConflictDetectedDto"/> 中的 RequestId 相同。
    /// 用于将此解决方案关联回正确的冲突上下文。
    /// </summary>
    [Required(ErrorMessage = "必须提供关联的请求 ID")]
    public string RequestId { get; set; } = null!;

    /// <summary>
    /// 发生冲突的 Block 的 ID (应与 <see cref="ConflictDetectedDto"/> 中的 BlockId 相同)。
    /// </summary>
    [Required(ErrorMessage = "必须提供 Block ID")]
    public string BlockId { get; set; } = null!;

    /// <summary>
    /// 经过用户确认或修改后的最终原子操作列表。
    /// 这些操作将应用于 Block，以完成工作流并将其状态转换为 Idle (或 Error)。
    /// 使用 <see cref="AtomicOperationRequestDto"/> 以便通过 SignalR 传输。
    /// </summary>
    [Required(ErrorMessage = "必须提供解决冲突后的指令列表")]
    public List<AtomicOperationRequestDto> ResolvedCommands { get; set; } = new();
}

