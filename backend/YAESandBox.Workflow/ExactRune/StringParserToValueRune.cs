using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.Config.RuneConfig;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Runtime.Processor;
using YAESandBox.Workflow.Runtime.Processor.RuneProcessor;
using YAESandBox.Workflow.VarSpec;
using static YAESandBox.Workflow.Runtime.Processor.TuumProcessor;

namespace YAESandBox.Workflow.ExactRune;

/// <summary>
/// “字符串解析为值”符文的运行时处理器。
/// </summary>
internal class StringParserToValueRuneProcessor(StringParserToValueRuneConfig config, ICreatingContext creatingContext)
    : NormalRuneProcessor<StringParserToValueRuneConfig, StringParserToValueRuneProcessor.StringParserToValueRuneDebugDto>(config,
        creatingContext)
{
    /// <inheritdoc />
    public override Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
    {
        // 1. 获取输入并初始化
        string inputText = tuumProcessorContent.GetTuumVar<string>(this.Config.InputVariableName) ?? string.Empty;
        this.DebugDto.InputText = inputText;

        var results = new Dictionary<string, object?>();
        var regexOptions = this.BuildRegexOptions();
        this.DebugDto.AppliedRegexOptions = regexOptions.ToString();

        // 2. 遍历所有提取规则
        foreach (var rule in this.Config.ExtractionRules)
        {
            var ruleDebugInfo = new RuleExecutionDebugInfo(rule.FieldName);
            this.DebugDto.ExtractionResults.Add(ruleDebugInfo);

            string? rawValue = null;
            bool matchSuccess = false;

            // 3. 执行正则匹配
            if (!string.IsNullOrEmpty(inputText) && !string.IsNullOrEmpty(rule.Pattern))
            {
                try
                {
                    var regex = new Regex(rule.Pattern, regexOptions, TimeSpan.FromSeconds(2));
                    var match = regex.Match(inputText);

                    if (match.Success && match.Groups["value"].Success)
                    {
                        rawValue = match.Groups["value"].Value;
                        matchSuccess = true;
                    }
                }
                catch (Exception ex)
                {
                    ruleDebugInfo.Error = $"正则执行失败: {ex.Message}";
                    // 如果正则本身有错，但规则不是必需的，我们可以继续处理其他规则
                    if (rule.IsRequired)
                    {
                        var error = new Error($"规则 '{rule.FieldName}' 的正则表达式 '{rule.Pattern}' 无效。", ex);
                        this.DebugDto.RuntimeError = error.ToDetailString();
                        return Result.Fail(error).AsCompletedTask();
                    }

                    // 继续循环
                    continue;
                }
            }

            ruleDebugInfo.WasMatchSuccess = matchSuccess;
            ruleDebugInfo.ExtractedRawValue = rawValue;

            // 4. 处理未匹配的情况和默认值
            if (!matchSuccess)
            {
                if (!string.IsNullOrEmpty(rule.DefaultValue))
                {
                    rawValue = rule.DefaultValue;
                }
                else if (rule.IsRequired)
                {
                    var error = Result.Fail($"必需的字段 '{rule.FieldName}' 未能从输入中匹配到，且没有提供默认值。");
                    this.DebugDto.RuntimeError = error.Message;
                    return error.AsCompletedTask();
                }
                else
                {
                    // 非必需字段，无匹配，无默认值，则该字段值为 null
                    results[rule.FieldName] = null;
                    ruleDebugInfo.FinalValue = null;
                    continue;
                }
            }

            // 5. 类型转换
            var (convertedValue, conversionError) = this.ConvertValue(rawValue, rule.FieldType);
            if (conversionError != null)
            {
                ruleDebugInfo.Error = conversionError;
                if (rule.IsRequired)
                {
                    var error = Result.Fail($"必需的字段 '{rule.FieldName}' 的值 '{rawValue}' 无法转换为类型 '{rule.FieldType}'。");
                    this.DebugDto.RuntimeError = error.Message;
                    return error.AsCompletedTask();
                }

                // 对于非必需字段，转换失败则设为null
                results[rule.FieldName] = null;
                ruleDebugInfo.FinalValue = null;
            }
            else
            {
                results[rule.FieldName] = convertedValue;
                ruleDebugInfo.FinalValue = convertedValue;
            }
        }

        // 6. 设置输出变量
        this.DebugDto.FinalOutput = results;
        tuumProcessorContent.SetTuumVar(this.Config.OutputVariableName, results);

        return Result.Ok().AsCompletedTask();
    }

    /// <summary>
    /// 根据配置构建 RegexOptions。
    /// </summary>
    private RegexOptions BuildRegexOptions()
    {
        var options = RegexOptions.None;
        if (this.Config.IgnoreCase) options |= RegexOptions.IgnoreCase;
        if (this.Config.Multiline) options |= RegexOptions.Multiline;
        if (this.Config.DotAll) options |= RegexOptions.Singleline; // DotAll 模式对应 C# 的 Singleline
        options |= RegexOptions.Compiled;
        return options;
    }

    /// <summary>
    /// 将提取出的字符串值转换为目标类型。
    /// </summary>
    private (object? value, string? error) ConvertValue(string? rawValue, ExtractedValueType targetType)
    {
        if (rawValue is null) return (null, null);

        switch (targetType)
        {
            case ExtractedValueType.String:
                return (rawValue, null);

            case ExtractedValueType.Integer:
                if (long.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out long longResult))
                {
                    return (longResult, null);
                }

                return (null, "无法解析为整数。");

            case ExtractedValueType.Float:
                if (double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double doubleResult))
                {
                    return (doubleResult, null);
                }

                return (null, "无法解析为浮点数。");

            case ExtractedValueType.Boolean:
                if (bool.TryParse(rawValue, out bool boolResult))
                {
                    return (boolResult, null);
                }

                // 支持 "1" / "0"
                if (rawValue == "1") return (true, null);
                if (rawValue == "0") return (false, null);
                return (null, "无法解析为布尔值。");

            default:
                return (rawValue, "未知的目标类型。");
        }
    }

    #region Debug DTOs

    /// <summary>
    /// 单条解析规则的执行调试信息。
    /// </summary>
    internal record RuleExecutionDebugInfo(string FieldName)
    {
        public bool WasMatchSuccess { get; set; }
        public string? ExtractedRawValue { get; set; }
        public object? FinalValue { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// 符文处理器的调试数据传输对象。
    /// </summary>
    internal record StringParserToValueRuneDebugDto : IRuneProcessorDebugDto
    {
        public string? InputText { get; set; }
        public string? AppliedRegexOptions { get; set; }
        public List<RuleExecutionDebugInfo> ExtractionResults { get; } = [];
        public Dictionary<string, object?>? FinalOutput { get; set; }
        public string? RuntimeError { get; set; }
    }

    #endregion
}

/// <summary>
/// “字符串解析为值”符文的配置。
/// </summary>
[ClassLabel("🔎值解析")]
internal record StringParserToValueRuneConfig : AbstractRuneConfig<StringParserToValueRuneProcessor>
{
    private const string GroupIO = "输入/输出";
    private const string GroupRegex = "全局正则选项";

    #region Main Config Properties

    [Required]
    [InlineGroup(GroupIO)]
    [Display(Name = "输入变量名", Description = "包含要解析的源文本的变量名。")]
    public string InputVariableName { get; init; } = "AiOutput";

    [Required]
    [InlineGroup(GroupIO)]
    [Display(Name = "输出变量名", Description = "用于存储解析结果（一个对象）的目标变量名。")]
    public string OutputVariableName { get; init; } = "parsedResult";

    [InlineGroup(GroupRegex)]
    [DefaultValue(true)]
    [Display(Name = "忽略大小写 (i)", Description = "执行不区分大小写的匹配。")]
    public bool IgnoreCase { get; init; } = true;

    [InlineGroup(GroupRegex)]
    [DefaultValue(false)]
    [Display(Name = "多行模式 (m)", Description = "使 ^ 和 $ 匹配行的开头和结尾。")]
    public bool Multiline { get; init; }

    [InlineGroup(GroupRegex)]
    [DefaultValue(true)]
    [Display(Name = "点号匹配所有 (s)", Description = "使点号 (.) 匹配包括换行符在内的所有字符。")]
    public bool DotAll { get; init; } = true;

    [Display(Name = "提取规则", Description = "定义如何从输入文本中提取每个字段。")]
    public List<ExtractionRule> ExtractionRules { get; init; } = [];

    #endregion

    #region Static Analysis

    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec() =>
        [new(this.InputVariableName, CoreVarDefs.String)];

    /// <inheritdoc />
    public override List<ProducedSpec> GetProducedSpec() =>
        // 输出是一个包含任意值的记录/字典
        [new(this.OutputVariableName, CoreVarDefs.RecordStringAny)];

    #endregion

    /// <inheritdoc />
    protected override StringParserToValueRuneProcessor ToCurrentRune(ICreatingContext creatingContext) => new(this, creatingContext);
}

/// <summary>
/// 定义单个字段的提取规则。
/// </summary>
public record ExtractionRule
{
    private const string ExtractionObject = "提取对象";

    [InlineGroup(ExtractionObject)]
    [Required(AllowEmptyStrings = false)]
    [Display(Name = "字段名", Description = "提取出的值在输出对象中的键名。")]
    public string FieldName { get; init; } = string.Empty;

    [InlineGroup(ExtractionObject)]
    [Required]
    [DefaultValue(ExtractedValueType.String)]
    [Display(Name = "字段类型", Description = "希望将提取出的值转换为哪种类型。")]
    [StringOptions(
        [
            nameof(ExtractedValueType.String),
            nameof(ExtractedValueType.Integer),
            nameof(ExtractedValueType.Float),
            nameof(ExtractedValueType.Boolean)
        ],
        [
            "字符串", "整数", "浮点数", "布尔值"
        ]
    )]
    public ExtractedValueType FieldType { get; init; } = ExtractedValueType.String;

    [Required(AllowEmptyStrings = false)]
    [DataType(DataType.MultilineText)]
    [Display(Name = "正则表达式", Description = "用于提取值的.NET正则表达式。必须包含一个名为 'value' 的捕获组，例如 '等级：(?<value>\\d+)'。")]
    public string Pattern { get; init; } = string.Empty;

    [Display(Name = "默认值 (可选)", Description = "如果正则表达式未匹配到任何内容，将使用此值。")]
    public string? DefaultValue { get; init; }

    [DefaultValue(false)]
    [Display(Name = "是否必需", Description = "如果勾选，当此字段无法匹配也无默认值时，整个符文将执行失败。")]
    public bool IsRequired { get; init; }
}

/// <summary>
/// 定义提取值的目标数据类型。
/// </summary>
public enum ExtractedValueType
{
    String,
    Integer,
    Float,
    Boolean
}