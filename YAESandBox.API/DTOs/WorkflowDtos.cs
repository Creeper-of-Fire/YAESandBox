using System.ComponentModel.DataAnnotations;
using YAESandBox.Core.Action;
using YAESandBox.Core.State; // For AtomicOperation

namespace YAESandBox.API.DTOs;

// --- WebSocket (SignalR) 消息 DTOs ---

public class TriggerWorkflowRequestDto
{
    [Required] public string RequestId { get; set; } = null!; // 用于追踪请求

    [Required] public string ParentBlockId { get; set; } = null!;

    [Required] public string WorkflowName { get; set; } = null!;

    public Dictionary<string, object?> Params { get; set; } = new();
}

public class ResolveConflictRequestDto
{
    [Required] public string RequestId { get; set; } = null!; // 关联原始触发请求

    [Required] public string BlockId { get; set; } = null!; // 发生冲突的 Block

    [Required] public List<AtomicOperation> ResolvedCommands { get; set; } = new(); // 用户解决冲突后的指令列表
    // 注意：这里直接用了 Core 的 AtomicOperation，也可以再创建一个 DTO
}

public class BlockStatusUpdateDto
{
    public string BlockId { get; set; } = null!;
    public BlockStatus Status { get; set; }
    public string? ParentBlockId { get; set; }
}

public class WorkflowUpdateDto
{
    public string RequestId { get; set; } = null!;
    public string BlockId { get; set; } = null!;
    public string UpdateType { get; set; } = "stream_chunk"; // 或 "progress", "log" 等
    public object? Data { get; set; } // 具体内容，如文本块、进度百分比
}

public class WorkflowCompleteDto
{
    public string RequestId { get; set; } = null!;
    public string BlockId { get; set; } = null!;
    public string ExecutionStatus { get; set; } = null!; // "success", "failure", "partial_failure"
    public string? FinalContent { get; set; } // 最终生成的完整内容

    public string? ErrorMessage { get; set; } // 如果失败
    // 可以添加其他结果信息
}

public class ConflictDetectedDto
{
    public string RequestId { get; set; } = null!; // 关联原始触发请求
    public string BlockId { get; set; } = null!;
    public List<AtomicOperation> ConflictingAiCommands { get; set; } = new();

    public List<AtomicOperation> ConflictingUserCommands { get; set; } = new();
    // 注意：这里也直接用了 Core 的 AtomicOperation
}

public class StateUpdateSignalDto
{
    public string BlockId { get; set; } = null!;
    // 可以考虑添加变更摘要信息，如果需要的话
    // public List<string> ChangedEntityIds { get; set; } = new();
}