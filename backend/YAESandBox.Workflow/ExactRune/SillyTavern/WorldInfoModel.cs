using System.Text.Json.Serialization;

namespace YAESandBox.Workflow.ExactRune.SillyTavern;

// 这些枚举用于在代码中提供清晰的语义，但不会直接参与JSON序列化/反序列化，
// 以保持与原始 "魔法数字" 的兼容性。

/// <summary>
/// 描述世界书条目的插入位置。
/// </summary>
file enum WorldInfoPosition
{
    BeforeCharDefs = 0,
    AfterCharDefs = 1,
    BeforeExampleMessages = 2,
    AfterExampleMessages = 3,
    TopOfAN = 4,
    BottomOfAN = 5,
    DepthBased = 6, // @ D
}

/// <summary>
/// 描述次要关键字 (Secondary Keys) 的匹配逻辑。
/// </summary>
file enum SelectiveLogic
{
    AndAny = 0,
    AndAll = 1,
    NotAny = 2,
    NotAll = 3,
}

/// <summary>
/// 当插入位置为 DepthBased 时，指定插入消息的角色。
/// </summary>
file enum MessageRole
{
    System = 0,
    User = 1,
    Assistant = 2,
}

/// <summary>
/// 代表一个完整的 SillyTavern 世界书 (World Info / Lorebook) 文件。
/// </summary>
public record SillyTavernWorldInfo
{
    /// <summary>
    /// 包含所有世界书条目的字典。
    /// 键是条目的唯一标识符（通常是数字字符串），值是条目对象。
    /// </summary>
    [JsonPropertyName("entries")]
    public IReadOnlyDictionary<string, WorldInfoEntry> Entries { get; init; } = new Dictionary<string, WorldInfoEntry>();
}

/// <summary>
/// 代表一个世界书 (World Info) 条目。
/// </summary>
public record WorldInfoEntry
{
    /// <summary>
    /// 条目的唯一ID。
    /// </summary>
    [JsonPropertyName("uid")]
    public int Uid { get; init; }

    /// <summary>
    /// 触发此条目的主关键字列表。支持正则表达式。
    /// </summary>
    [JsonPropertyName("key")]
    public IReadOnlyList<string> Keys { get; init; } = new List<string>();

    /// <summary>
    /// 触发此条目的次要/过滤关键字列表。其行为由 SelectiveLogic 定义。
    /// </summary>
    [JsonPropertyName("keysecondary")]
    public IReadOnlyList<string> SecondaryKeys { get; init; } = new List<string>();

    /// <summary>
    /// 条目的内容，将在激活时插入到上下文中。
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 条目的注释或标题，仅为用户提供方便，不影响AI。
    /// </summary>
    [JsonPropertyName("comment")]
    public string Comment { get; init; } = string.Empty;

    /// <summary>
    /// 插入顺序。数字越大，优先级越高，越靠近提示词的末尾。
    /// </summary>
    [JsonPropertyName("order")]
    public int InsertionOrder { get; init; }

    /// <summary>
    /// 插入位置。这是一个整数，其含义由 <see cref="WorldInfoPosition"/> 枚举定义。
    /// 例如: 4 代表插入到 Author's Note 顶部。
    /// </summary>
    [JsonPropertyName("position")]
    public int Position { get; init; }

    /// <summary>
    /// 当 Position 为 DepthBased (6) 时，指定插入消息的角色。
    /// 0 = System, 1 = User, 2 = Assistant。
    /// </summary>
    [JsonPropertyName("role")]
    public int? MessageRole { get; init; }

    /// <summary>
    /// 插入深度。仅当 Position 为 DepthBased (6) 时生效。
    /// 0=最末尾, 1=倒数第二条消息前, etc.
    /// 映射自 JSON 的 "depth" 字段。
    /// </summary>
    [JsonPropertyName("depth")]
    public int? InsertionDepth { get; init; }

    /// <summary>
    /// 扫描深度覆盖。定义为此条目在历史记录中回溯多少条消息进行匹配。
    /// 如果为 null，则使用全局设置。
    /// 映射自 JSON 的 "scanDepth" 字段。
    /// </summary>
    [JsonPropertyName("scanDepth")]
    public int? ScanDepthOverride { get; init; }

    /// <summary>
    /// 激活概率 (0-100)。100 表示每次触发都会插入。
    /// </summary>
    [JsonPropertyName("probability")]
    public int Probability { get; init; } = 100;

    /// <summary>
    /// 是否启用激活概率检查。
    /// </summary>
    [JsonPropertyName("useProbability")]
    public bool UseProbability { get; init; } = true;

    /// <summary>
    /// 是否禁用此条目。
    /// </summary>
    [JsonPropertyName("disable")]
    public bool IsDisabled { get; init; }

    /// <summary>
    /// (策略) 是否为常驻条目。如果为 true，则忽略关键字，始终尝试插入。
    /// 对应于UI中的🔵(蓝色圆圈)。
    /// </summary>
    [JsonPropertyName("constant")]
    public bool IsConstant { get; init; }

    /// <summary>
    /// (策略) 是否允许通过向量相似度匹配激活。
    /// 对应于UI中的🔗(链接)。
    /// </summary>
    [JsonPropertyName("vectorized")]
    public bool IsVectorized { get; init; }

    /// <summary>
    /// (策略) 是否使用次要关键字进行过滤。
    /// </summary>
    [JsonPropertyName("selective")]
    public bool UseSecondaryKeys { get; init; }

    /// <summary>
    /// 次要关键字的逻辑。这是一个整数，其含义由 <see cref="SelectiveLogic"/> 枚举定义。
    /// 0 = AND ANY, 1 = AND ALL, 2 = NOT ANY, 3 = NOT ALL.
    /// </summary>
    [JsonPropertyName("selectiveLogic")]
    public int SelectiveLogic { get; init; }

    /// <summary>
    /// (递归) 此条目不会被其他条目递归激活。
    /// </summary>
    [JsonPropertyName("excludeRecursion")]
    public bool ExcludeFromRecursion { get; init; }

    /// <summary>
    /// (递归) 此条目被激活后，不会再触发其他条目。
    /// </summary>
    [JsonPropertyName("preventRecursion")]
    public bool PreventFurtherRecursion { get; init; }

    /// <summary>
    /// (递归) 此条目只在递归扫描阶段被激活。
    /// </summary>
    [JsonPropertyName("delayUntilRecursion")]
    public bool DelayUntilRecursion { get; init; }

    /// <summary>
    /// 所属的包含组。用于在多个同组条目被触发时，只选择一个插入。
    /// </summary>
    [JsonPropertyName("group")]
    public string Group { get; init; } = string.Empty;

    /// <summary>
    /// 在包含组选择中，是否优先选择 "InsertionOrder" 更高的条目，而不是随机选择。
    /// </summary>
    [JsonPropertyName("groupOverride")]
    public bool PrioritizeInGroup { get; init; }

    /// <summary>
    /// 在包含组中进行随机选择时的权重。
    /// </summary>
    [JsonPropertyName("groupWeight")]
    public int GroupWeight { get; init; } = 100;

    // --- 以下字段为完整性而包含，但可能在无状态模型中用途有限 ---

    /// <summary>
    /// 局部覆盖全局的 "区分大小写" 设置。
    /// </summary>
    [JsonPropertyName("caseSensitive")]
    public bool? CaseSensitiveOverride { get; init; }

    /// <summary>
    /// 局部覆盖全局的 "匹配整个单词" 设置。
    /// </summary>
    [JsonPropertyName("matchWholeWords")]
    public bool? MatchWholeWordsOverride { get; init; }

    /// <summary>
    /// 局部覆盖全局的 "使用群组评分" 设置。
    /// </summary>
    [JsonPropertyName("useGroupScoring")]
    public bool? UseGroupScoringOverride { get; init; }
}