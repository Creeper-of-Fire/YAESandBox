using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace YAESandBox.Workflow.Rune.SillyTavern;

/// <summary>
/// 用于在 C# 代码中清晰地表示 injection_position 的逻辑，而不会影响 JSON 序列化。
/// </summary>
file enum TavernInjectionPosition
{
    /// <summary>
    /// 值为 0。表示提示词遵循 prompt_order 中的顺序，简单地插入到前一个提示词之后。
    /// 在此模式下，injection_depth 和 injection_order 字段将被忽略。
    /// </summary>
    Sequential = 0,

    /// <summary>
    /// 值为 1。表示启用“深度注入”模式，提示词将被注入到聊天历史记录的特定位置。
    /// 此模式下，必须使用 injection_depth 和 injection_order 来确定精确位置。
    /// </summary>
    HistoryRelative = 1,
}

/// <summary>
/// 代表一个完整的 SillyTavern 提示词预设文件。
/// 目前我们只关注提示词定义（Prompts）和它们的顺序（PromptOrder）。
/// </summary>
public record SillyTavernPreset
{
    /// <summary>
    /// 预设中定义的所有提示词项和标记的列表。
    /// </summary>
    [JsonPropertyName("prompts")]
    [Required]
    public List<PromptItem> Prompts { get; init; } = [];

    /// <summary>
    /// 定义了不同角色（通过 character_id 区分）的提示词使用顺序。
    /// </summary>
    [JsonPropertyName("prompt_order")]
    [Required]
    public List<PromptOrderSetting> PromptOrder { get; init; } = [];
}

/// <summary>
/// 代表 "prompts" 数组中的一个条目。
/// 这个类型统一了两种提示词：
/// 1. 实际带有内容的提示词（例如 'main', 'nsfw'）。
/// 2. 仅作为位置标记的提示词（例如 'chatHistory', 'scenario'），其 'Marker' 属性为 true。
/// </summary>
public record PromptItem
{
    /// <summary>
    /// 提示词的唯一标识符，用于在 'prompt_order' 中引用。
    /// 例如："main", "chatHistory", "c10aa67a-4768-4eb8-88eb-ce69f8dd0ea4"
    /// </summary>
    [JsonPropertyName("identifier")]
    [Required]
    public string Identifier { get; init; } = string.Empty;

    /// <summary>
    /// 在 SillyTavern UI 中显示的友好名称。
    /// 例如："全部要求", "Chat History", "设定"
    /// </summary>
    [JsonPropertyName("name")]
    [Required]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 是否为系统提示词。
    /// </summary>
    [JsonPropertyName("system_prompt")]
    [Required]
    public bool SystemPrompt { get; init; }

    /// <summary>
    /// 指示这是否是一个位置标记，而不是一个实际的提示词。
    /// 如果为 true，则 'role', 'content' 等字段通常不存在。
    /// </summary>
    [JsonPropertyName("marker")]
    [Required]
    public bool Marker { get; init; }

    /// <summary>
    /// 角色。对于内容提示词是必须的。
    /// 例如："system", "user", "assistant"
    /// </summary>
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    /// <summary>
    /// 提示词的具体内容，支持变量替换。
    /// 对于标记提示词，此字段为 null。
    /// </summary>
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    /// <summary>
    /// 注入位置模式。这个整数值在 JSON 中被保留以实现完美兼容。
    /// <list type="bullet">
    /// <item><c>0</c> (<see cref="TavernInjectionPosition.Sequential"/>): 在 `prompt_order` 中按顺序放置，忽略深度和顺序值。</item>
    /// <item><c>1</c> (<see cref="TavernInjectionPosition.HistoryRelative"/>): 启用深度注入，此时 `injection_depth` 和 `injection_order` 生效。</item>
    /// </list>
    /// </summary>
    [JsonPropertyName("injection_position")]
    public int? InjectionPosition { get; init; }

    /// <summary>
    /// 注入深度，仅在 `injection_position` 为 1 时生效。
    /// 定义了提示词相对于聊天历史记录末尾的位置。
    /// <list type="bullet">
    /// <item><c>0</c>: 在最后一条消息之后。</item>
    /// <item><c>1</c>: 在最后一条消息之前。</item>
    /// <item><c>2</c>: 在倒数第二条消息之前，以此类推。</item>
    /// </list>
    /// </summary>
    [JsonPropertyName("injection_depth")]
    public int? InjectionDepth { get; init; }

    /// <summary>
    /// 注入顺序，仅在 `injection_position` 为 1 时生效。
    /// 当多个提示词注入到相同的 `injection_depth` 时，此值用于排序。
    /// 值从小到大排列。具有相同 `injection_order` 值的提示词之间的顺序是未定义的。
    /// </summary>
    [JsonPropertyName("injection_order")]
    public int? InjectionOrder { get; init; }

    /// <summary>
    /// 注入触发器（具体用途不详，但定义以备完整性）。
    /// </summary>
    [JsonPropertyName("injection_trigger")]
    public List<string>? InjectionTrigger { get; init; } = [];


    /// <summary>
    /// 是否禁止覆盖。不知道干啥用的。
    /// </summary>
    [JsonPropertyName("forbid_overrides")]
    public bool? ForbidOverrides { get; init; }

    /// <summary>
    /// 提示词是否被启用。这个字段在 'prompts' 列表中有时会出现。
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool? Enabled { get; init; }
}

/// <summary>
/// 代表 "prompt_order" 数组中的一个条目，它为一个特定的角色 ID 定义了提示词顺序。
/// </summary>
public record PromptOrderSetting
{
    /// <summary>
    /// 角色ID。几乎所有预设都使用 100000 或 100001 作为默认/通用配置。
    /// </summary>
    [JsonPropertyName("character_id")]
    [Required]
    public long CharacterId { get; init; }

    /// <summary>
    /// 定义了该角色启用的提示词及其顺序的列表。
    /// </summary>
    [JsonPropertyName("order")]
    [Required]
    public List<OrderItem> Order { get; init; } = [];
}

/// <summary>
/// 代表 "order" 数组中的一个条目，指定了一个提示词及其启用状态。
/// </summary>
public record OrderItem
{
    /// <summary>
    /// 引用 'prompts' 列表中的一个 PromptItem 的 'identifier'。
    /// </summary>
    [JsonPropertyName("identifier")]
    [Required]
    public string Identifier { get; init; } = string.Empty;

    /// <summary>
    /// 指示此提示词在当前顺序中是否被启用。
    /// </summary>
    [JsonPropertyName("enabled")]
    [Required]
    public bool Enabled { get; init; }
}