using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Rune;
using YAESandBox.Workflow.Tuum;
using YAESandBox.Workflow.VarSpec;
using static YAESandBox.Plugin.TextParser.Rune.RegexGeneratorRuneProcessor;

namespace YAESandBox.Plugin.TextParser.Rune;

/// <summary>
/// “正则生成”符文的运行时处理器。
/// </summary>
public class RegexGeneratorRuneProcessor(RegexGeneratorRuneConfig config)
    : INormalRune<RegexGeneratorRuneConfig, RegexGeneratorDebugDto>
{
    /// <inheritdoc />
    public RegexGeneratorRuneConfig Config { get; } = config;

    /// <inheritdoc />
    public RegexGeneratorDebugDto DebugDto { get; } = new();

    /// <summary>
    /// 执行正则生成或替换逻辑。
    /// </summary>
    public Task<Result> ExecuteAsync(TuumProcessor.TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
    {
        // 1. 获取输入文本
        object? rawInputValue = tuumProcessorContent.GetTuumVar(this.Config.InputVariableName);
        string inputText = rawInputValue?.ToString() ?? string.Empty;
        this.DebugDto.InputText = inputText;

        if (string.IsNullOrWhiteSpace(inputText) || string.IsNullOrEmpty(this.Config.Pattern))
        {
            tuumProcessorContent.SetTuumVar(this.Config.OutputVariableName, string.Empty);
            return Task.FromResult(Result.Ok());
        }

        try
        {
            // 根据配置构建 RegexOptions
            var regexOptions = this.BuildRegexOptions();
            this.DebugDto.AppliedOptions = regexOptions.ToString();

            string finalOutput;
            // 根据操作模式选择不同的逻辑分支
            if (this.Config.OperationMode == RegexOperationMode.Generate)
            {
                // --- 生成模式 ---
                finalOutput = this.HandleGenerateMode(inputText, regexOptions);
            }
            else // RegexOperationMode.Replace
            {
                // --- 替换模式 ---
                finalOutput = this.HandleReplaceMode(inputText, regexOptions);
            }

            this.DebugDto.FinalOutput = finalOutput;
            tuumProcessorContent.SetTuumVar(this.Config.OutputVariableName, finalOutput);

            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            this.DebugDto.RuntimeError = $"正则执行失败: {ex.Message}";
            return Task.FromResult(Result.Fail($"正则操作符文执行失败: {ex.Message}").ToResult());
        }
    }

    /// <summary>
    /// 处理生成模式的逻辑。
    /// </summary>
    private string HandleGenerateMode(string inputText, RegexOptions options)
    {
        var allMatches = Regex.Matches(inputText, this.Config.Pattern, options);
        this.DebugDto.FoundMatchCount = allMatches.Count;

        // 根据 MaxMatches 限制处理的匹配项数量
        var matchesToProcess = this.Config.MaxMatches > 0
            ? allMatches.Take(this.Config.MaxMatches)
            : allMatches;

        var generatedParts = new List<string>();
        foreach (var match in matchesToProcess)
        {
            if (!match.Success)
                continue;

            // 使用 Result 方法进行模板替换
            string part = match.Result(this.Config.OutputTemplate);
            generatedParts.Add(part);
        }

        this.DebugDto.ProcessedMatchCount = generatedParts.Count;
        this.DebugDto.GeneratedParts = generatedParts;

        // 使用连接符拼接所有部分
        return string.Join(this.Config.JoinSeparator, generatedParts);
    }

    /// <summary>
    /// 处理替换模式的逻辑。
    /// </summary>
    private string HandleReplaceMode(string inputText, RegexOptions options)
    {
        // 1. 创建一个 Regex 实例。这是使用带 count 的 Replace 方法的正确途径。
        //    我们可以在构造函数中同时指定选项和超时。
        var regex = new Regex(this.Config.Pattern, options, TimeSpan.FromSeconds(5));

        // 2. 为了调试，先计算总共能匹配多少个。
        this.DebugDto.FoundMatchCount = regex.Count(inputText);

        // 3. 如果 MaxMatches > 0，则调用实例方法的 Replace 重载，传入 count 参数。
        if (this.Config.MaxMatches > 0)
        {
            this.DebugDto.ProcessedMatchCount = Math.Min(this.DebugDto.FoundMatchCount, this.Config.MaxMatches);
            return regex.Replace(inputText, this.Config.OutputTemplate, this.Config.MaxMatches);
        }

        // 4. 否则，调用不带 count 的重载来替换所有匹配项。
        this.DebugDto.ProcessedMatchCount = this.DebugDto.FoundMatchCount;
        return regex.Replace(inputText, this.Config.OutputTemplate);
    }

    /// <summary>
    /// 辅助方法，根据配置构建 RegexOptions。
    /// </summary>
    private RegexOptions BuildRegexOptions()
    {
        var options = RegexOptions.None;
        if (this.Config.IgnoreCase) options |= RegexOptions.IgnoreCase;
        if (this.Config.Multiline) options |= RegexOptions.Multiline;
        if (this.Config.Dotall) options |= RegexOptions.Singleline;
        // 默认启用编译以提高性能
        options |= RegexOptions.Compiled;
        return options;
    }

    /// <summary>
    /// 正则操作符文的调试 DTO。
    /// </summary>
    public record RegexGeneratorDebugDto : IRuneProcessorDebugDto
    {
        public string? InputText { get; set; }
        public string? AppliedOptions { get; set; }
        public int FoundMatchCount { get; set; }
        public int? ProcessedMatchCount { get; set; }

        /// <summary>
        /// 【仅生成模式】下填充，表示每个匹配项生成的独立部分。
        /// </summary>
        public List<string>? GeneratedParts { get; set; }

        public string? FinalOutput { get; set; }
        public string? RuntimeError { get; set; }
    }
}

/// <summary>
/// “正则操作”符文的配置。
/// 使用正则表达式，对文本执行生成或替换操作。
/// </summary>
[ClassLabel("⚙️正则操作")]
[RenderWithVueComponent("RegexGeneratorEditor")]
public record RegexGeneratorRuneConfig : AbstractRuneConfig<RegexGeneratorRuneProcessor>
{
    #region 配置项

    /// <summary>
    /// 指定从哪个枢机变量中读取源文本。
    /// </summary>
    [Required]
    [Display(Name = "输入变量名", Description = "包含源文本的变量。")]
    public required string InputVariableName { get; init; }

    /// <summary>
    /// 定义此符文是“生成”新内容还是“替换”原文内容。
    /// </summary>
    [Required]
    [Display(Name = "操作模式", Description = "选择是“生成”新文本，还是在原文上“替换”匹配项。")]
    [StringOptions(
        [nameof(RegexOperationMode.Replace), nameof(RegexOperationMode.Generate)],
        ["替换", "生成"]
    )]
    [DefaultValue(RegexOperationMode.Replace)]
    public RegexOperationMode OperationMode { get; init; } = RegexOperationMode.Generate;

    /// <summary>
    /// 用于查找匹配项的 .NET 正则表达式，支持命名捕获组。
    /// </summary>
    [Required]
    [DataType(DataType.MultilineText)]
    [Display(Name = "正则表达式", Description = ".NET 正则表达式，用于查找所有匹配项。")]
    [DefaultValue(@"姓名：(?<name>\S+)\s+年龄：(?<age>\d+)")]
    public required string Pattern { get; init; }

    /// <summary>
    /// 为每个匹配项生成输出的模板。
    /// 使用 ${name} 引用命名捕获组，或 $1, $2 引用数字捕获组。
    /// </summary>
    [Required]
    [DataType(DataType.MultilineText)]
    [Display(Name = "输出模板", Description = "为每个匹配项生成文本的模板。")]
    [DefaultValue("- 角色名: ${name}, 年龄: ${age}岁。")]
    public required string OutputTemplate { get; init; }

    // --- 新增的高级选项 ---
    [Display(Name = "忽略大小写 (i)", Description = "执行不区分大小写的匹配。")]
    [DefaultValue(false)]
    public bool IgnoreCase { get; init; }

    [Display(Name = "多行模式 (m)", Description = "使 ^ 和 $ 匹配一行的开头和结尾，而不仅仅是整个字符串的开头和结尾。")]
    [DefaultValue(false)]
    public bool Multiline { get; init; }

    [Display(Name = "点号匹配所有 (s)", Description = "使点号 (.) 匹配包括换行符在内的所有字符。也称为 DotAll 模式。")]
    [DefaultValue(false)]
    public bool Dotall { get; init; }

    [Display(Name = "最大处理次数", Description = "设置最大匹配或替换的次数。0 或负数表示不限制。")]
    [DefaultValue(0)]
    public int MaxMatches { get; init; }

    /// <summary>
    /// 【仅生成模式】当有多个匹配项时，用于连接每个生成文本的字符串。
    /// </summary>
    [Display(Name = "连接符", Description = "【仅生成模式】下生效，用于拼接多个匹配结果的分隔符。")]
    [DefaultValue("\n")]
    public string JoinSeparator { get; init; } = "\n";

    /// <summary>
    /// 指定将最终拼接好的结果存入哪个枢机变量。
    /// </summary>
    [Required]
    [Display(Name = "输出变量名", Description = "用于存储最终生成文本的目标变量。")]
    public required string OutputVariableName { get; init; }

    #endregion

    #region 静态分析与转换

    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec() => [new(this.InputVariableName, CoreVarDefs.String)];

    /// <inheritdoc />
    public override List<ProducedSpec> GetProducedSpec() => [new(this.OutputVariableName, CoreVarDefs.String)];

    /// <inheritdoc />
    protected override RegexGeneratorRuneProcessor ToCurrentRune(WorkflowRuntimeService workflowRuntimeService) => new(this);

    #endregion
}

/// <summary>
/// 定义正则符文的操作模式。
/// </summary>
public enum RegexOperationMode
{
    /// <summary>
    /// 在原始文本中，查找并替换所有匹配项。
    /// </summary>
    Replace,

    /// <summary>
    /// 为每个匹配项生成新内容，并用连接符拼接。
    /// </summary>
    Generate
}