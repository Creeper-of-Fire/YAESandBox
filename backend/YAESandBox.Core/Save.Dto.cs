// --- Persistence DTOs ---

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

// For GameState (conceptually)

// For EntityType, BaseEntity (for CoreFields)

namespace YAESandBox.Core;
// Namespace matches BlockManager persistence location

/// <summary>
/// 代表 YAESandBox 完整状态存档文件的顶层结构。
/// 用于 JSON 序列化和反序列化。
/// </summary>
public record ArchiveDto
{
    /// <summary>
    /// 存储所有 Block 的信息。键是 Block ID (string)，值是 <see cref="BlockDto"/>。
    /// </summary>
    [JsonPropertyName("blocks")]
    public Dictionary<string, BlockDto> Blocks { get; init; } = new();

    /// <summary>
    /// 存储由前端提供并在保存时传入的“盲存”数据。
    /// 后端不解析此数据，仅在加载时原样返回给前端。
    /// 类型为 object? 以便接受任何有效的 JSON 值。
    /// </summary>
    [JsonPropertyName("blindStorage")]
    public object? BlindStorage { get; init; }

    /// <summary>
    /// 存档文件的版本号，用于未来可能的格式迁移。
    /// </summary>
    [JsonPropertyName("archiveVersion")]
    public string ArchiveVersion { get; init; } = "1.0"; // Default version
}

/// <summary>
/// 用于持久化单个 Block 信息的 DTO。
/// 包含了 Block 的结构、内容、元数据、状态快照等。
/// </summary>
public record BlockDto
{
    /// <summary>
    /// Block 的唯一标识符。
    /// </summary>
    [JsonPropertyName("id")]
    [Required]
    public required string BlockId { get; init; }

    /// <summary>
    /// 父 Block 的 ID。根节点的此值为 null。
    /// </summary>
    [JsonPropertyName("parentId")]
    public string? ParentBlockId { get; init; }

    /// <summary>
    /// 此 Block 的直接子 Block 的 ID 列表。
    /// </summary>
    [JsonPropertyName("childrenIds")]
    public List<string> ChildrenIds { get; init; } = [];

    /// <summary>
    /// Block 的主要内容字符串（例如 AI 生成的文本、配置等）。
    /// </summary>
    [JsonPropertyName("content")]
    public string BlockContent { get; init; } = string.Empty;

    /// <summary>
    /// 存储触发block时所使用的工作流名称。
    /// </summary>
    [JsonPropertyName("workFlowName")]
    public string WorkFlowName { get; init; } = string.Empty;

    /// <summary>
    /// （仅父 Block 存储）触发此 Block 的某个子 Block 时所使用的参数。
    /// 注意：当前设计只保存最后一次触发子节点时的参数。
    /// </summary>
    [JsonPropertyName("triggeredChildParams")]
    public Dictionary<string, string> TriggeredChildParams { get; init; } = new();

    /// <summary>
    /// 被触发时使用的参数。用于重新生成之类的。
    /// </summary>
    [JsonPropertyName("triggeredParams")]
    public Dictionary<string, string> TriggeredParams { get; init; } = new();

    /// <summary>
    /// 与 Block 相关的元数据字典。键值对均为字符串。
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; init; } = new();

    /// <summary>
    /// 持久化的 GameState。存储为键值对字典，值类型为 object?。
    /// </summary>
    [JsonPropertyName("gameState")]
    public Dictionary<string, object?> GameState { get; init; } = new(); // 直接存字典

    /// <summary>
    /// 持久化的 WorldState 快照。
    /// 键是快照类型 ("wsInput", "wsPostAI", "wsPostUser")。值为对应的 <see cref="WorldStateDto"/>。
    /// 注意：`wsTemp` 不会被持久化。加载时，如果 Block 状态非 Idle，会被恢复为 Idle 状态。
    /// `wsInput` 必须存在（根节点除外）。`wsPostAI` 和 `wsPostUser` 可能为 null。
    /// </summary>
    [JsonPropertyName("worldStates")]
    public Dictionary<string, WorldStateDto?> WorldStates { get; init; } = new();
}

/// <summary>
/// 用于持久化单个 WorldState 快照的 DTO。
/// 包含该时间点所有实体的信息。
/// </summary>
public record WorldStateDto
{
    /// <summary>
    /// 持久化的 Item 实体。键是 Item 的 EntityId，值是 <see cref="EntityDto"/>。
    /// </summary>
    [JsonPropertyName("items")]
    public Dictionary<string, EntityDto> Items { get; init; } = new();

    /// <summary>
    /// 持久化的 Character 实体。键是 Character 的 EntityId，值是 <see cref="EntityDto"/>。
    /// </summary>
    [JsonPropertyName("characters")]
    public Dictionary<string, EntityDto> Characters { get; init; } = new();

    /// <summary>
    /// 持久化的 Place 实体。键是 Place 的 EntityId，值是 <see cref="EntityDto"/>。
    /// </summary>
    [JsonPropertyName("places")]
    public Dictionary<string, EntityDto> Places { get; init; } = new();
}

/// <summary>
/// 用于持久化单个实体信息的 DTO。
/// 包含了实体的所有属性。
/// </summary>
public record EntityDto
{
    // EntityId 从 WorldStateDto 的字典 Key 中获取。
    // EntityType 从 WorldStateDto 的字典属性名 (items, characters, places) 推断。

    /// <summary>
    /// 存储实体的所有属性（包括核心属性如 IsDestroyed 和其他动态属性）的字典。
    /// 键是属性名 (string)，值是属性值 (object?)。
    /// 值的实际类型在序列化时保留 (string, int, bool, List, Dictionary, TypedID etc.)。
    /// 加载时需要特殊处理 (<see cref="PersistenceMapper.DeserializeObjectValue"/>) 来正确恢复复杂类型。
    /// </summary>
    [JsonPropertyName("attributes")]
    public Dictionary<string, object?> Attributes { get; init; } = new();
}