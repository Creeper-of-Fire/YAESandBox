using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.Rune;

namespace YAESandBox.Workflow.Tuum;

/// <summary>
/// 祝祷的配置
/// </summary>
public record TuumConfig
{
    /// <summary>
    /// 名字
    /// </summary>
    [Required]
    [HiddenInForm(true)]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 是否被启用，默认为True
    /// </summary>
    [Required]
    [HiddenInForm(true)]
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 唯一的 ID，在拷贝时也需要更新
    /// </summary>
    [Required]
    [HiddenInForm(true)]
    public required string ConfigId { get; init; }

    /// <summary>
    /// 按顺序执行的符文列表。
    /// TuumProcessor 在执行时会严格按照此列表的顺序执行符文。
    /// </summary>
    [Required]
    [HiddenInForm(true)]
    public List<AbstractRuneConfig> Runes { get; init; } = [];

    /// <summary>
    /// 定义了此祝祷可被连接的【输入端点】。
    /// Key: 祝祷内部期望的变量名 (供符文消费)。
    /// Value: 输入端点的名称 (供外部连接使用)。
    /// </summary>
    /// <example>
    /// "initial_query": "customer_question"
    /// 这意味着，一个名为 "customer_question" 的输入端点，
    /// 会将数据提供给祝祷内部名为 "initial_query" 的变量。
    /// 多个内部变量可以连接到同一个输入端点。
    /// </example>
    [Required]
    public Dictionary<string, string> InputMappings { get; init; } = [];

    /// <summary>
    /// 定义了此祝祷可被连接的【输出端点】。
    /// Key: 输出端点的名称 (供外部连接使用)。
    /// Value: 提供数据的祝祷内部变量名 (由符文产生)。
    /// </summary>
    /// <example>
    /// "final_greeting": "rune_A_raw_text"
    /// 这定义了一个名为 "final_greeting" 的输出端点，
    /// 其数据来源于祝祷内部名为 "rune_A_raw_text" 的变量。
    /// 一个内部变量可以被映射到多个输出端点。
    /// </example>
    [Required]
    public Dictionary<string, string> OutputMappings { get; init; } = [];
}