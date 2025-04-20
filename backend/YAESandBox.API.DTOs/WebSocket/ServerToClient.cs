using YAESandBox.Depend;

namespace YAESandBox.API.DTOs.WebSocket;

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
    public string BlockId { get; set; } = null!;

    /// <summary>
    /// Block 的新状态码 (<see cref="BlockStatusCode"/>: Idle, Loading, ResolvingConflict, Error)。
    /// </summary>
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
    /// <summary>
    /// (关键区分) 目标 UI 元素或逻辑区域的 ID。
    /// - 如果为 **null** 或空字符串：表示这是一个 **主工作流** 更新，应更新与 <cref name="ContextBlockId"/> 关联的主要显示区域。
    /// - 如果 **非 null**：表示这是一个 **微工作流** 更新，应更新 ID 与此值匹配的特定 UI 元素或区域。
    /// </summary>
    public string? TargetElementId { get; set; }

    /// <summary>
    /// (可选) 指示前端应使用哪个脚本或渲染器来处理 <cref name="Content"/>。
    /// 主要用于主 Block 显示，例如指定使用 Markdown 渲染器、自定义图表脚本等。
    /// 微工作流通常不需要此字段。
    /// </summary>
    public string? ScriptId { get; set; }

    /// <summary>
    /// (可选，仅当 <cref name="UpdateMode"/> 为 <see cref="UpdateMode.Incremental"/> 时相关)
    /// 指示增量更新的类型，例如 "JsonPatch", "DiffMatchPatch", "SimpleAppend" 等。
    /// 具体值和解释取决于前后端约定。
    /// </summary>
    public string? IncrementalType { get; set; }

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
/// <param name="BlockId">发生冲突的 Block 的 ID。</param>
/// <param name="RequestId">关联的原始工作流请求 ID。</param>
/// <param name="AiCommands">工作流（AI）生成的 **完整** 原子操作列表。</param>
/// <param name="UserCommands">用户在 Loading 期间提交的 **完整** 原子操作列表（可能包含因 Create/Create 冲突而被自动重命名的操作）。</param>
/// <param name="ConflictingAiCommands">导致 **阻塞性冲突** (Modify/Modify 同一属性) 的 AI 原子操作子集。</param>
/// <param name="ConflictingUserCommands">导致 **阻塞性冲突** (Modify/Modify 同一属性) 的用户原子操作子集。</param>
public record ConflictDetectedDto(
    string BlockId,
    string RequestId,
    List<AtomicOperationRequestDto> AiCommands,
    List<AtomicOperationRequestDto> UserCommands,
    List<AtomicOperationRequestDto> ConflictingAiCommands,
    List<AtomicOperationRequestDto> ConflictingUserCommands);

/// <summary>
/// (服务器 -> 客户端)
/// 一个轻量级信号，通知客户端指定 Block 的状态 (WorldState 或 GameState) 可能已发生变化。
/// 鼓励客户端根据需要重新获取该 Block 的详细信息或相关实体的最新状态。
/// </summary>
/// <param name="BlockId">状态可能已发生变化的 Block 的 ID。</param>
/// <param name="ChangedFields">（可选）如果变化是由原子操作引起的，这里可以包含受影响的 Block 数据字段的枚举值，以便前端进行更精细的更新。如果为 null 或空，表示通用状态变更。</param>
/// <param name="ChangedEntityIds">（可选）如果变化是由原子操作引起的，这里可以包含受影响的实体的 ID 列表，以便前端进行更精细的更新。如果为 null 或空，表示未知具体实体。</param>
public record StateUpdateSignalDto(string BlockId, List<BlockDataFields>? ChangedFields = null, List<string>? ChangedEntityIds = null);

/// <summary>
/// 指示 <see cref="DisplayUpdateDto"/> 消息在流式传输过程中的状态。
/// </summary>
public enum StreamStatus
{
    /// <summary>
    /// 工作流仍在处理中，后续可能还会有 <see cref="DisplayUpdateDto"/> 消息。
    /// </summary>
    Streaming,

    /// <summary>
    /// 工作流已成功完成，这是此 RequestId 的最后一条消息（对于该 TargetElementId，如果是微工作流）。
    /// </summary>
    Complete,

    /// <summary>
    /// 工作流执行过程中发生错误而中止。这通常是此 RequestId 的最后一条消息。
    /// <see cref="DisplayUpdateDto.Content"/> 可能包含错误信息。
    /// </summary>
    Error
}

/// <summary>
/// 指示 <see cref="DisplayUpdateDto.Content"/> 的更新方式。
/// </summary>
public enum UpdateMode
{
    /// <summary>
    /// <see cref="DisplayUpdateDto.Content"/> 包含目标区域的完整内容，应替换现有内容。
    /// </summary>
    FullSnapshot,

    /// <summary>
    /// <see cref="DisplayUpdateDto.Content"/> 包含对现有内容的增量更改。
    /// 需要根据 <see cref="DisplayUpdateDto.IncrementalType"/> 来应用更改。
    /// </summary>
    Incremental
}