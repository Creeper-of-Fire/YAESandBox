using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.Rune;
using YAESandBox.Workflow.VarSpec;

namespace YAESandBox.Workflow.Tuum;

/// <summary>
/// 枢机的配置
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
    /// 定义了内部变量所需数据的来源，即从哪个外部输入端点获取。
    /// 此结构以“内部需求”为起点，描述了“我这个变量，需要的数据从哪里来”。
    /// 这种设计更符合用户配置时的直观逻辑。
    /// </summary>
    /// <remarks>
    /// 核心特性与校验规则：
    /// 1. Key (内部变量名) 在字典中必须唯一。这天然地保证了每个内部变量只有一个数据源。
    /// 2. 多个不同的内部变量 (Key) 可以映射到同一个外部输入端点 (Value)，从而实现数据复用。
    /// 3. 【校验】当多个内部变量映射到同一个外部端点时，它们的类型必须相互兼容。
    /// </remarks>
    /// <example>
    /// 假设配置为:
    /// {
    ///   "initial_query": "customer_question",
    ///   "log_entry": "customer_question"
    /// }
    /// 这意味着：
    /// - 内部变量 "initial_query" 的数据来源于外部端点 "customer_question"。
    /// - 内部变量 "log_entry" 的数据也来源于外部端点 "customer_question"。
    /// 系统会自动创建一个名为 "customer_question" 的外部输入端点。
    /// </example>
    [Required]
    public Dictionary<string, string> InputMappings { get; init; } = [];

    /// <summary>
    /// 定义了此枢机的【内部变量】如何驱动【输出端点】。
    /// Key: 提供数据的枢机内部变量名 (由符文产生)。
    /// Value: 由该内部变量驱动的外部输出端点名称列表。
    /// </summary>
    /// <remarks>
    /// 校验规则：
    /// 1. Key (内部变量名) 在此字典中必须唯一。
    /// 2. 一个外部输出端点名在所有 Value 列表中总共只能出现一次，以保证每个输出端点只有一个数据源。
    /// </remarks>
    /// <example>
    /// "rune_A_raw_text": [ "final_greeting", "summary_output" ]
    /// 这定义了内部变量 "rune_A_raw_text" 的数据，
    /// 将会同时流向名为 "final_greeting" 和 "summary_output" 的两个外部输出端点。
    /// </example>
    [Required]
    public Dictionary<string, HashSet<string>> OutputMappings { get; init; } = [];
}