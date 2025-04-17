// --- Persistence DTOs ---

using System.Text.Json.Serialization;

// For EntityType, TypedID
// For GameState? (Maybe define GameStateDto here too)

// For BlockStatusCode (if needed, though we load as Idle)

namespace YAESandBox.Core;

/// <summary>
/// 存档文件的顶层结构。
/// </summary>
public class ArchiveDto
{
    /// <summary>
    /// 存储所有 Block 的信息。键是 Block ID。
    /// </summary>
    [JsonPropertyName("blocks")]
    public Dictionary<string, BlockDto> Blocks { get; set; } = new();

    /// <summary>
    /// 存储前端需要的盲存数据。
    /// </summary>
    [JsonPropertyName("blindStorage")]
    public object? BlindStorage { get; set; }

    // 可以添加版本号或其他元数据
    [JsonPropertyName("archiveVersion")] public string ArchiveVersion { get; set; } = "1.0";
}

/// <summary>
/// 用于持久化的 Block 信息。
/// </summary>
public class BlockDto
{
    [JsonPropertyName("id")] public string BlockId { get; set; } = null!;

    [JsonPropertyName("parentId")] public string? ParentBlockId { get; set; }

    [JsonPropertyName("childrenIds")] public List<string> ChildrenIds { get; set; } = [];

    [JsonPropertyName("content")] public string BlockContent { get; set; } = string.Empty;

    [JsonPropertyName("metadata")] public Dictionary<string, string> Metadata { get; set; } = new();

    [JsonPropertyName("triggeredChildParams")]
    public Dictionary<string, object?> TriggeredChildParams { get; set; } = new();

    /// <summary>
    /// 持久化的 GameState。
    /// </summary>
    [JsonPropertyName("gameState")]
    public Dictionary<string, object?> GameState { get; set; } = new(); // 直接存字典

    /// <summary>
    /// 持久化的 WorldState 快照。键是快照类型 ("wsInput", "wsPostAI", "wsPostUser")。
    /// </summary>
    [JsonPropertyName("worldStates")]
    public Dictionary<string, WorldStateDto?> WorldStates { get; set; } = new();
}

/// <summary>
/// 用于持久化的 WorldState 信息。
/// </summary>
public class WorldStateDto
{
    /// <summary>
    /// 持久化的实体。键是 Entity ID。
    /// </summary>
    [JsonPropertyName("items")]
    public Dictionary<string, EntityDto> Items { get; set; } = new();

    [JsonPropertyName("characters")] public Dictionary<string, EntityDto> Characters { get; set; } = new();

    [JsonPropertyName("places")] public Dictionary<string, EntityDto> Places { get; set; } = new();
}

/// <summary>
/// 用于持久化的实体信息。
/// </summary>
public class EntityDto
{
    // EntityId 和 EntityType 从 WorldStateDto 的字典结构和 Key 中隐含，或者显式添加
    // [JsonPropertyName("entityId")]
    // public string EntityId { get; set; } = null!;

    // [JsonPropertyName("entityType")]
    // public EntityType EntityType { get; set; }

    /// <summary>
    /// 存储实体的所有属性（包括核心属性如 IsDestroyed 和动态属性）。
    /// </summary>
    [JsonPropertyName("attributes")]
    public Dictionary<string, object?> Attributes { get; set; } = new();
}