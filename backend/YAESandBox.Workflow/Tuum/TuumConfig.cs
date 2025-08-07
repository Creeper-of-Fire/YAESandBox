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
    /// 定义了此枢机可被连接的【输入端点】及其驱动的内部变量。
    /// Key: 外部输入端点的名称 (供外部连接使用)。
    /// Value: 由该输入端点提供数据的内部变量名列表 (供符文消费)。
    /// </summary>
    /// <remarks>
    /// 校验规则：
    /// 1. Key (外部输入端点名) 在此字典中必须唯一。
    /// 2. 一个内部变量名在所有 Value 列表中总共只能出现一次，以保证每个内部变量只有一个数据源。
    /// </remarks>
    /// <example>
    /// "customer_question": [ "initial_query", "log_entry" ]
    /// 这意味着，一个名为 "customer_question" 的外部输入端点，
    /// 会将它的数据同时提供给枢机内部的 "initial_query" 和 "log_entry" 两个变量。
    /// </example>
    [Required]
    public Dictionary<string, List<string>> InputMappings { get; init; } = [];

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
    public Dictionary<string, List<string>> OutputMappings { get; init; } = [];
}

/// <summary>
/// 定义一个 Tuum 的输入端点。
/// </summary>
public record TuumInputEndpoint(ConsumedSpec EndpointSpec, List<string> MappedInternalVars);

/// <summary>
/// 定义一个 Tuum 的输出端点。
/// </summary>
public record TuumOutputEndpoint(ProducedSpec EndpointSpec, List<string> MappedInternalVars);