using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Schema.SchemaProcessor;

namespace YAESandBox.Plugin.Rune.TextParser.Rune;

/// <summary>
/// 一个可重用的配置记录，封装了通用的文本处理（提取/替换）操作的设置。
/// 任何需要实现此模式的符文配置都可以通过组合来包含此记录。
/// </summary>
public record TextOperationConfig
{
    /// <summary>
    /// 指定要处理的数据类型是普通文本还是提示词列表。
    /// </summary>
    [Required]
    [Display(Name = "输入数据类型", Description = "选择要处理的数据是普通文本，还是一个提示词列表（将对列表中的每条内容进行处理）。")]
    [StringOptions(
        [
            nameof(InputDataTypeEnum.String),
            nameof(InputDataTypeEnum.PromptList)
        ],
        [
            "文本 (String)",
            "提示词列表 (PromptList)"
        ]
    )]
    [DefaultValue(InputDataTypeEnum.String)]
    public InputDataTypeEnum InputDataType { get; init; } = InputDataTypeEnum.String;

    /// <summary>
    /// 指定从哪个枢机变量中读取源数据。
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    [Display(Name = "输入变量名", Description = "包含源数据的变量名。")]
    public string InputVariableName { get; init; } = string.Empty;

    /// <summary>
    /// 定义此符文是提取信息还是替换内容。
    /// </summary>
    [Required]
    [Display(Name = "操作模式", Description = "选择是“提取”匹配的内容，还是“替换”它们。")]
    [StringOptions(
        [
            nameof(OperationModeEnum.Extract),
            nameof(OperationModeEnum.Replace)
        ],
        [
            "提取",
            "替换"
        ]
    )]
    [DefaultValue(OperationModeEnum.Extract)]
    public OperationModeEnum OperationMode { get; init; } = OperationModeEnum.Extract;

    /// <summary>
    /// 替换模板，用于生成替换后的内容。
    /// </summary>
    [Display(Name = "替换模板", Description = "【替换模式】下生效。使用 ${match} 占位符代表匹配到的原始内容。")]
    public string ReplacementTemplate { get; init; } = "替换后的内容：${match}";

    /// <summary>
    /// 如果选择器匹配到多个元素，定义如何格式化返回结果。
    /// </summary>
    [Display(Name = "输出格式", Description =
        "【提取模式】定义输出的最终形态。\n" +
        "• **仅第一个**: 只输出第一个匹配项的内容。如果无匹配，则输出为空。类型：`String?`\n" +
        "• **作为列表**: 始终输出一个列表，其中包含所有匹配项的内容。如果无匹配，则输出空列表。类型：`String[]`\n" +
        "• **作为JSON字符串**: 始终输出一个包含所有匹配项的JSON数组字符串。类型：`String`"
    )]
    [StringOptions(
        [
            nameof(ReturnFormatEnum.First),
            nameof(ReturnFormatEnum.AsList),
            nameof(ReturnFormatEnum.AsJsonString)
        ],
        [
            "仅第一个",
            "作为列表",
            "作为JSON字符串"
        ]
    )]
    [DefaultValue(ReturnFormatEnum.First)]
    public ReturnFormatEnum ReturnFormat { get; init; } = ReturnFormatEnum.First;

    /// <summary>
    /// 指定将结果存入哪个枢机变量。
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    [Display(Name = "输出变量名", Description = "用于存储结果的目标变量。")]
    public string OutputVariableName { get; init; } = string.Empty;
}

/// <summary>
/// 定义符文处理的数据类型
/// </summary>
public enum InputDataTypeEnum
{
    String,
    PromptList
}

/// <summary>
/// 定义返回结果格式
/// </summary>
public enum ReturnFormatEnum
{
    First,
    AsList,
    AsJsonString
}

/// <summary>
/// 定义文本操作符文是执行“提取”还是“替换”。
/// </summary>
public enum OperationModeEnum
{
    /// <summary>
    /// 从输入文本中提取信息并输出。
    /// </summary>
    Extract,

    /// <summary>
    /// 在输入文本中查找匹配项并替换它们，然后输出修改后的全文。
    /// </summary>
    Replace
}