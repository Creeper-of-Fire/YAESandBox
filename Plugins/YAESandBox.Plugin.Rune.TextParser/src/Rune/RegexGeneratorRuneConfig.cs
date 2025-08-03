using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Workflow;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Rune;
using YAESandBox.Workflow.Tuum;

namespace YAESandBox.Plugin.TextParser.Rune;

/// <summary>
/// “正则生成”符文的运行时处理器。
/// </summary>
public class RegexGeneratorRuneProcessor(RegexGeneratorRuneConfig config)
    : IWithDebugDto<RegexGeneratorRuneProcessor.RegexGeneratorDebugDto>, INormalRune
{
    private RegexGeneratorRuneConfig Config { get; } = config;

    /// <inheritdoc />
    public RegexGeneratorDebugDto DebugDto { get; } = new();

    /// <summary>
    /// 执行正则生成逻辑。
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
            // 2. 查找所有匹配项
            var matches = Regex.Matches(inputText, this.Config.Pattern);
            this.DebugDto.MatchCount = matches.Count;

            // 3. 对每个匹配项应用模板
            var generatedParts = new List<string>();
            foreach (Match match in matches)
            {
                if (!match.Success)
                    continue;
                string part = match.Result(this.Config.OutputTemplate);
                generatedParts.Add(part);
            }

            this.DebugDto.GeneratedParts = generatedParts;

            // 4. 使用连接符拼接所有部分
            string finalOutput = string.Join(this.Config.JoinSeparator, generatedParts);
            this.DebugDto.FinalOutput = finalOutput;

            // 5. 将结果写入祝祷上下文
            tuumProcessorContent.SetTuumVar(this.Config.OutputVariableName, finalOutput);

            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            this.DebugDto.RuntimeError = $"正则执行失败: {ex.Message}";
            return Task.FromResult(Result.Fail($"正则生成符文执行失败: {ex.Message}").ToResult());
        }
    }

    /// <summary>
    /// 正则生成符文的调试 DTO。
    /// </summary>
    public record RegexGeneratorDebugDto : IRuneProcessorDebugDto
    {
        public string? InputText { get; set; }
        public int MatchCount { get; set; }
        public List<string> GeneratedParts { get; set; } = [];
        public string? FinalOutput { get; set; }
        public string? RuntimeError { get; set; }
    }
}

/// <summary>
/// “正则生成”符文的配置。
/// 遍历文本中所有匹配正则表达式的片段，并使用模板生成新的内容。
/// </summary>
[ClassLabel("⚙️ 正则生成")]
// [RenderWithVueComponent("RegexGeneratorEditor")] // 同样先注释掉
public record RegexGeneratorRuneConfig : AbstractRuneConfig<RegexGeneratorRuneProcessor>
{
    #region 配置项

    /// <summary>
    /// 指定从哪个祝祷变量中读取源文本。
    /// </summary>
    [Required]
    [Display(Name = "输入变量名", Description = "包含源文本的变量。")]
    public required string InputVariableName { get; init; }

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

    /// <summary>
    /// 当有多个匹配项时，用于连接每个生成文本的字符串。
    /// </summary>
    [Display(Name = "连接符", Description = "用于拼接多个匹配结果的分隔符。")]
    [DefaultValue("\n")]
    public string JoinSeparator { get; init; } = "\n";

    /// <summary>
    /// 指定将最终拼接好的结果存入哪个祝祷变量。
    /// </summary>
    [Required]
    [Display(Name = "输出变量名", Description = "用于存储最终生成文本的目标变量。")]
    public required string OutputVariableName { get; init; }

    #endregion

    #region 静态分析与转换

    /// <inheritdoc />
    public override List<string> GetConsumedVariables() => [this.InputVariableName];

    /// <inheritdoc />
    public override List<string> GetProducedVariables() => [this.OutputVariableName];

    /// <inheritdoc />
    protected override RegexGeneratorRuneProcessor ToCurrentRune(WorkflowRuntimeService workflowRuntimeService) => new(this);

    #endregion
}