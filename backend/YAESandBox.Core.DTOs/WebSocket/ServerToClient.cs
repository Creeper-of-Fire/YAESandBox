using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend;
using YAESandBox.Workflow.Abstractions;

namespace YAESandBox.Core.DTOs.WebSocket;
// --- 服务器推送到客户端的消息 DTOs ---

/// <summary>
/// (服务器 -> 客户端)
/// 通知客户端某个 Block 的状态码发生了变化。
/// </summary>
public record BlockStatusUpdateDto
{
    /// <summary>
    /// 状态发生变化的 Block 的 ID。
    /// </summary>
    [Required]
    public required string BlockId { get; set; }

    /// <summary>
    /// Block 的新状态码 (<see cref="BlockStatusCode"/>: Idle, Loading, ResolvingConflict, Error)。
    /// </summary>
    [Required]
    public BlockStatusCode StatusCode { get; set; }
}

/// <summary>
/// (服务器 -> 客户端)
/// 携带由工作流（主工作流或微工作流）生成或处理的内容，用于更新前端显示。
/// </summary>
/// <param name="RequestId">关联的原始工作流请求 ID (<see cref="TriggerMainWorkflowRequestDto.RequestId"/> 或 <see cref="TriggerMicroWorkflowRequestDto.RequestId"/>)。</param>
/// <param name="ContextBlockId">主要关联的 Block ID。对于主工作流，这是被更新或新创建的 Block。对于微工作流，这通常是触发时的上下文 Block。</param>
/// <param name="Content">需要显示或处理的内容字符串。可能是完整的文本、HTML 片段、JSON 数据，或增量更新指令。</param>
/// <param name="StreamingStatus">指示此消息在流式传输中的状态 (<see cref="StreamStatus"/>: Streaming, Complete, Error)。</param>
/// <param name="UpdateMode">指示 <paramref name="Content"/> 是完整内容快照还是增量更新 (<see cref="UpdateMode"/>)。默认为 <see cref="UpdateMode.FullSnapshot"/>。</param>
public record DisplayUpdateDto(
    string RequestId,
    string ContextBlockId,
    string Content,
    StreamStatus StreamingStatus,
    UpdateMode UpdateMode = UpdateMode.FullSnapshot
)
{
    /// <summary>关联的原始工作流请求 ID (<see cref="TriggerMainWorkflowRequestDto.RequestId"/> 或 <see cref="TriggerMicroWorkflowRequestDto.RequestId"/>)。</summary>
    [Required]
    public string RequestId { get; init; } = RequestId;

    /// <summary>主要关联的 Block ID。对于主工作流，这是被更新或新创建的 Block。对于微工作流，这通常是触发时的上下文 Block。</summary>
    [Required]
    public string ContextBlockId { get; init; } = ContextBlockId;

    /// <summary>需要显示或处理的内容字符串。可能是完整的文本、HTML 片段、JSON 数据，或增量更新指令。</summary>
    [Required]
    public string Content { get; init; } = Content;

    /// <summary>指示此消息在流式传输中的状态 (<see cref="StreamStatus"/>: Streaming, Complete, Error)。</summary>
    [Required]
    public StreamStatus StreamingStatus { get; init; } = StreamingStatus;

    /// <summary>指示 <see cref="Content"/> 是完整内容快照还是增量更新 (<see cref="UpdateMode"/>)。默认为 <see cref="UpdateMode.FullSnapshot"/>。</summary>
    [Required]
    public UpdateMode UpdateMode { get; init; } = UpdateMode;

    /// <summary>
    /// (关键区分) 目标 UI 元素或逻辑区域的 ID。
    /// - 如果为 **null** 或空字符串：表示这是一个 **主工作流** 更新，应更新与 <cref name="ContextBlockId"/> 关联的主要显示区域。
    /// - 如果 **非 null**：表示这是一个 **微工作流** 更新，应更新 ID 与此值匹配的特定 UI 元素或区域。
    /// </summary>
    public string? TargetElementId { get; set; }

    /// <summary>
    /// (可选) 消息的序列号，用于处理乱序或重复的消息。
    /// 客户端可以根据需要实现排序和去重逻辑。
    /// </summary>
    public long? SequenceNumber { get; set; }
}

/// <summary>
/// (服务器 -> 客户端)
/// 当主工作流执行完成后，检测到 AI 生成的指令与用户在 Loading 状态下提交的指令存在冲突时发送。
/// 前端应使用此信息向用户展示冲突详情，并提供解决冲突的界面。
/// </summary>
public record ConflictDetectedDto
{
    // /// <summary>
    // /// 发生冲突的 Block 的 ID。
    // /// </summary>
    // [Required]
    // public required string BlockId { get; init; }
    //
    // /// <summary>
    // /// 关联的原始工作流请求 ID。
    // /// </summary>
    // [Required]
    // public required string RequestId { get; init; }

    /// <summary>工作流（AI）生成的 **完整** 原子操作列表。</summary>
    [Required]
    public required List<AtomicOperationRequestDto> AiCommands { get; init; }

    /// <summary>用户在 Loading 期间提交的 **完整** 原子操作列表（可能包含因 Create/Create 冲突而被自动重命名的操作）。</summary>
    [Required]
    public required List<AtomicOperationRequestDto> UserCommands { get; init; }

    /// <summary>导致 **阻塞性冲突** (Modify/Modify 同一属性) 的 AI 原子操作子集。</summary>
    [Required]
    public required List<AtomicOperationRequestDto> ConflictingAiCommands { get; init; }

    /// <summary>导致 **阻塞性冲突** (Modify/Modify 同一属性) 的用户原子操作子集。</summary>
    [Required]
    public required List<AtomicOperationRequestDto> ConflictingUserCommands { get; init; }
}

/// <summary>
/// (服务器 -> 客户端)
/// 一个轻量级信号，通知客户端指定 Block 的状态 (WorldState 或 GameState) 可能已发生变化。
/// 鼓励客户端根据需要重新获取该 Block 的详细信息或相关实体的最新状态。
/// </summary>
public record BlockUpdateSignalDto
{
    /// <summary>状态可能已发生变化的 Block 的 ID。</summary>
    [Required]
    public required string BlockId { get; init; }

    /// <summary>（可选）包含受影响的 Block 数据字段的枚举值，以便前端进行更精细的更新。如果为 null 或空，表示通用状态变更。</summary>
    public List<BlockDataFields>? ChangedFields { get; init; }

    /// <summary>（可选）如果变化是由原子操作引起的，这里可以包含受影响的实体的 ID 列表，以便前端进行更精细的更新。如果为 null 或空，表示未知具体实体。</summary>
    public List<string>? ChangedEntityIds { get; init; }
}