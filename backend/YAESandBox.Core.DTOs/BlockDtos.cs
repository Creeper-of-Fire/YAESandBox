using System.ComponentModel.DataAnnotations;
using YAESandBox.Core.DTOs.WebSocket;
using YAESandBox.Depend;

// For BlockStatusCode

namespace YAESandBox.Core.DTOs;

/// <summary>
/// 用于 API 响应，表示单个 Block 的详细信息（不包含 WorldState）。
/// </summary>
public record BlockDetailDto
{
    /// <summary>
    /// Block 的唯一标识符。
    /// </summary>
    [Required]
    public required string BlockId { get; init; }

    /// <summary>
    /// Block 当前的状态码 (例如 Idle, Loading, ResolvingConflict, Error)。
    /// </summary>
    public BlockStatusCode? StatusCode { get; init; }

    /// <summary>
    /// Block 的主要文本内容 (例如 AI 生成的文本、配置等)。
    /// </summary>
    public string? BlockContent { get; init; }

    /// <summary>
    /// 与 Block 相关的元数据字典 (键值对均为字符串)。
    /// </summary>
    public Dictionary<string, string>? Metadata { get; init; }

    /// <summary>
    /// 冲突信息（如果有冲突）
    /// </summary>
    public ConflictDetectedDto? ConflictDetected { get; init; }

    /// <summary>
    /// 工作流相关的信息
    /// </summary>
    public WorkflowDto? WorkflowInfo { get; init; }

    // 注意：WsInput, WsPostAI, WsPostUser, WsTemp, ChildrenInfo, ParentBlockId 不应直接通过 这个DTO 暴露
}

/// <summary>
/// 工作流 的 DTO。
/// </summary>
public record WorkflowDto
{
    /// <summary>
    /// 存储触发block时所使用的工作流名称。
    /// </summary>
    [Required]
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>
    /// （仅父 Block 存储）触发此 Block 的某个子 Block 时所使用的参数。
    /// 注意：当前设计只保存最后一次触发子节点时的参数。
    /// </summary>
    [Required]
    public Dictionary<string, string> TriggeredChildParams { get; set; } = new();

    /// <summary>
    /// 被触发时使用的参数。用于重新生成之类的。
    /// </summary>
    [Required]
    public Dictionary<string, string> TriggeredParams { get; set; } = new();
}

/// <summary>
/// 用于标识 Block 中可能过时的字段。
/// 临时举措：看到ParentBlockId和ChildrenInfo时，进行一次拓扑更新。以后这个逻辑可能会迁移到专门的通知。
/// </summary>
public enum BlockDataFields
{
    ParentBlockId,
    BlockContent,
    Metadata,
    ChildrenInfo,
    WorldState,
    GameState
}

/// <summary>
/// 用于通过 PATCH 请求部分更新 Block 的内容和元数据。
/// 任何设置为 null 的属性表示不修改该部分。
/// </summary>
public record UpdateBlockDetailsDto
{
    /// <summary>
    /// (可选) 要设置的新的 Block 内容。
    /// 如果为 null，则不修改 BlockContent。
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// (可选) 要更新或移除的元数据键值对。
    /// - Key: 要操作的元数据键。
    /// - Value:
    ///   - 如果为非 null 字符串: 添加或更新该键的值。
    ///   - 如果为 null: 从元数据中移除该键。
    /// 如果整个字典为 null，则不修改 Metadata。
    /// </summary>
    public Dictionary<string, string?>? MetadataUpdates { get; init; }
}

/// <summary>
/// 表示扁平化拓扑结构中的单个节点信息。
/// </summary>
public class BlockTopologyNodeDto
{
    /// <summary>
    /// Block 的唯一标识符。
    /// </summary>
    [Required]
    public required string BlockId { get; set; }

    /// <summary>
    /// 父 Block 的 ID。如果为根节点，则为 null。
    /// </summary>
    public string? ParentBlockId { get; set; }
}