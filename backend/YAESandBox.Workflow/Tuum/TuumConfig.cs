using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Schema.Attributes;
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
    /// 定义了此祝祷如何将其内部变量暴露到工作流的全局变量池。
    /// Key: 全局变量名 (在工作流中使用的名字)
    /// Value: 祝祷内部的变量名 (由符文产生的名字)
    /// </summary>
    /// <example>
    /// "final_greeting": "rune_A_raw_text"
    /// 这意味着，将此祝祷内部名为 "rune_A_raw_text" 的变量，
    /// 以 "final_greeting" 的名字发布到全局。
    /// </example>
    [Required]
    public Dictionary<string, string> OutputMappings { get; init; } = [];
    
    /// <summary>
    /// 定义了此祝祷如何从工作流的全局变量池获取输入，并映射到祝祷内部使用的变量名。
    /// Key: 祝祷内部期望的变量名 (符文消费的名字)
    /// Value: 全局变量名 (在工作流中可用的名字)
    /// </summary>
    /// <example>
    /// "initial_query": "customer_question"
    /// 这意味着，将全局变量池中的 "initial_query" 变量，
    /// 作为名为 "customer_question" 的输入提供给此祝祷内部的符文使用。
    /// </example>
    [Required]
    public Dictionary<string, string> InputMappings { get; init; } = [];
}