using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using YAESandBox.Core.Action;
using YAESandBox.Core.Block;
using YAESandBox.Core.State; // For AtomicOperation

namespace YAESandBox.API.DTOs;

// --- WebSocket (SignalR) 消息 DTOs ---
/// <summary>
/// 用于触发主工作流 (创建新 Block)
/// </summary>
public record TriggerMainWorkflowRequestDto
{
    /// <summary>
    /// 唯一的请求 ID，用于追踪特定的工作流调用。
    /// </summary>
    [Required]
    public string RequestId { get; set; } = null!; // 用于追踪请求

    /// <summary>
    /// 父 Block
    /// </summary>
    [Required]
    public string ParentBlockId { get; set; } = null!;

    /// <summary>
    /// 主工作流名称
    /// </summary>
    [Required]
    public string WorkflowName { get; set; } = null!;

    /// <summary>
    /// 传递给主工作流的参数
    /// </summary>
    public Dictionary<string, object?> Params { get; set; } = new();
}

/// <summary>
/// 用于触发微工作流 (不创建新 Block)
/// </summary>
public record TriggerMicroWorkflowRequestDto
{
    /// <summary>
    /// 唯一的请求 ID，用于追踪特定的工作流调用。
    /// </summary>
    [Required]
    public string RequestId { get; set; } = null!;

    /// <summary>
    /// 当前操作的上下文 Block，尽管必须提供但是不一定用得到。
    /// </summary>
    [Required]
    public string ContextBlockId { get; set; } = null!; // 当前操作的上下文 Block

    /// <summary>
    /// 目标 UI 元素/控件的 ID，用于标识要更新的 UI 。
    /// 应该由前端自己定义，并且原样发回给前端。
    /// </summary>
    [Required]
    public string TargetElementId { get; set; } = null!;

    /// <summary>
    /// 要调用的微工作流名称
    /// </summary>
    [Required]
    public string WorkflowName { get; set; } = null!;

    /// <summary>
    /// 传递给微工作流的参数
    /// </summary>
    public Dictionary<string, object?> Params { get; set; } = new();
}

/// <summary>
/// 已经解决了解决冲突的请求。前端已经完成冲突解决后，把这个发给后端。
/// </summary>
public record ResolveConflictRequestDto
{
    /// <summary>
    /// 关联原始触发请求
    /// </summary>
    [Required]
    public string RequestId { get; set; } = null!;

    /// <summary>
    /// 发生冲突的 Block
    /// </summary>
    [Required]
    public string BlockId { get; set; } = null!;

    /// <summary>
    /// 用户解决冲突后的指令列表
    /// </summary>
    [Required]
    public List<AtomicOperationRequestDto> ResolvedCommands { get; set; } = new();
    // 注意：这里直接用了 Core 的 AtomicOperation，也可以再创建一个 DTO
}

/// <summary>
/// Block的状态更新DTO，用于更新block的状态。
/// </summary>
public record BlockStatusUpdateDto
{
    /// <summary>
    /// Block的ID
    /// </summary>
    public string BlockId { get; set; } = null!;

    /// <summary>
    /// 状态码
    /// </summary>
    public BlockStatusCode StatusCode { get; set; }
}

/// <summary>
/// 显示DTO，前端接受这个内容用于处理工作流造成的显示更新。
/// </summary>
/// <param name="RequestId">唯一的请求 ID，用于追踪特定的工作流调用 (主或微)。</param>
/// <param name="ContextBlockId">主要关联的 Block ID。对于主工作流，这是被更新的 Block。对于微工作流，这通常是用户触发操作时所在的上下文 Block。</param>
/// <param name="Content">需要显示或填充的内容。</param>
/// <param name="StreamingStatus">指示当前消息是流的一部分，还是流的结束（成功或失败）。
/// 这对于没有 Block 状态码可依赖的微工作流至关重要。
/// 对于主工作流，它也表明内容流何时结束。</param>
/// <param name="UpdateMode">指示 Content 是完整快照还是增量。默认为完整快照</param>
public record DisplayUpdateDto(
    string RequestId,
    string ContextBlockId,
    string Content,
    StreamStatus StreamingStatus,
    UpdateMode UpdateMode = UpdateMode.FullSnapshot
)
{
    /// <summary>
    /// (可选) 前端 UI 元素或逻辑区域的特定 ID。
    /// 如果此值非 null，表示这是一个针对特定控件的微工作流更新。
    /// 如果为 null，表示这是一个针对 ContextBlockId 的主显示区域更新。
    /// </summary>
    public string? TargetElementId { get; set; }

    /// <summary>
    /// (可选) 辅助解析/渲染脚本的 ID。
    /// 主要用于主 Block 显示，微工作流通常不需要。
    /// </summary>
    public string? ScriptId { get; set; }

    //目前用不到这个内容
    /// <summary>
    /// 增量类型。比如是JsonPatch还是普通增量。
    /// </summary>
    public string? IncrementalType { get; set; }

    /// <summary>
    /// 序列号，用于排序和去重。
    /// </summary>
    public long? SequenceNumber { get; set; }
}

/// <summary>
/// 检测出冲突而发送的DTO，前端接受这个内容用于处理冲突。
/// // 注意：这里也直接用了 Core 的 AtomicOperation
/// </summary>
/// <param name="BlockId">关联原始触发请求</param>
/// <param name="RequestId">发生冲突的 Block</param>
/// <param name="AiCommands">完整的AI指令</param>
/// <param name="UserCommands">完整的用户指令</param>
/// <param name="ConflictingAiCommands">发生冲突的AI指令</param>
/// <param name="ConflictingUserCommands">发生冲突的用户指令</param>
public record ConflictDetectedDto(
    string BlockId,
    string RequestId,
    List<AtomicOperationRequestDto> AiCommands,
    List<AtomicOperationRequestDto> UserCommands,
    List<AtomicOperationRequestDto> ConflictingAiCommands,
    List<AtomicOperationRequestDto> ConflictingUserCommands);

/// <summary>
/// 状态更新信号的DTO，前端接受这个内容用于更新状态。
/// </summary>
/// <param name="BlockId"></param>
/// <param name="ChangedEntityIds"></param>
public record StateUpdateSignalDto(string BlockId, List<string> ChangedEntityIds);

/// <summary>
/// 流式传输的状态
/// </summary>
public enum StreamStatus
{
    /// <summary>
    /// 流仍在进行中
    /// </summary>
    Streaming,

    /// <summary>
    /// 流已成功结束
    /// </summary>
    Complete,

    /// <summary>
    /// 流因错误而中止
    /// </summary>
    Error
}

/// <summary>
/// 更新模式。
/// 用于标识内容是完整还是增量更新。
/// </summary>
public enum UpdateMode
{
    /// <summary>
    /// 完整快照
    /// </summary>
    FullSnapshot,

    /// <summary>
    /// 增量更新
    /// </summary>
    Incremental
}