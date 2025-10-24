using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Core.Config.RuneConfig;
using YAESandBox.Workflow.Core.DebugDto;
using YAESandBox.Workflow.Core.Runtime.Processor;
using YAESandBox.Workflow.Core.Runtime.Processor.RuneProcessor;
using YAESandBox.Workflow.Core.VarSpec;
using YAESandBox.Workflow.Schema;

namespace YAESandBox.Plugin.TextParser.Rune;

/// <summary>
/// “正则生成”符文的运行时处理器。
/// </summary>
internal class RegexParserRuneProcessor(RegexParserRuneConfig config,ICreatingContext creatingContext)
    : NormalRuneProcessor<RegexParserRuneConfig, RegexParserRuneProcessor.RegexParserDebugDto>(config, creatingContext)
{

    /// <summary>
    /// 执行正则生成或替换逻辑。
    /// </summary>
    /// <inheritdoc />
    public override Task<Result> ExecuteAsync(TuumProcessor.TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(this.Config.Pattern))
        {
            // 如果没有提供正则表达式，则不执行任何操作，直接返回空结果
            object emptyResult = this.Config.TextOperation.OperationMode == OperationModeEnum.Replace
                ? string.Empty
                : TextOperationHelper.FormatOutput([], this.Config.TextOperation.ReturnFormat);
            tuumProcessorContent.SetTuumVar(this.Config.TextOperation.OutputVariableName, emptyResult);
            return Result.Ok().AsCompletedTask();
        }

        try
        {
            // 1. 获取输入并构建 DTO
            object? rawInputValue = tuumProcessorContent.GetTuumVar(this.Config.TextOperation.InputVariableName);
            TextProcessingInput input;
            if (this.Config.TextOperation.InputDataType == InputDataTypeEnum.PromptList && rawInputValue is List<RoledPromptDto> prompts)
            {
                input = TextProcessingInput.FromPromptList(prompts);
                this.DebugDto.InputText = JsonSerializer.Serialize(prompts, YaeSandBoxJsonHelper.JsonSerializerOptions);
            }
            else
            {
                string inputText = rawInputValue?.ToString() ?? string.Empty;
                input = TextProcessingInput.FromString(inputText);
                this.DebugDto.InputText = inputText;
            }

            this.DebugDto.AppliedOptions = this.BuildRegexOptions().ToString();

            // 2. 调用通用辅助类，传入正则专属的提取和替换委托
            object finalOutput = TextOperationHelper.Process(
                input,
                this.Config.TextOperation.OperationMode,
                this.Extractor,
                this.Replacer,
                this.Config.TextOperation.ReturnFormat
            );

            // 3. 更新调试信息并设置输出
            this.DebugDto.FinalOutput = finalOutput;
            tuumProcessorContent.SetTuumVar(this.Config.TextOperation.OutputVariableName, finalOutput);

            return Result.Ok().AsCompletedTask();
        }
        catch (Exception ex)
        {
            this.DebugDto.RuntimeError = $"正则执行失败。{ex.ToFormattedString()}";
            return Result.Fail("正则解析符文执行失败。",ex).AsCompletedTask();
        }
    }

    /// <summary>
    /// 提取逻辑 (Extractor Delegate): 查找所有匹配项，并使用模板生成一个字符串列表。
    /// </summary>
    private List<string> Extractor(string inputText)
    {
        var regex = new Regex(this.Config.Pattern, this.BuildRegexOptions(), TimeSpan.FromSeconds(5));
        var allMatches = regex.Matches(inputText);
        this.DebugDto.FoundMatchCount += allMatches.Count;

        var matchesToProcess = this.Config.MaxMatches > 0
            ? allMatches.Take(this.Config.MaxMatches)
            : allMatches;

        var generatedParts = matchesToProcess
            .Where(m => m.Success)
            .Select(match => match.Result(this.Config.TextOperation.ReplacementTemplate))
            .ToList();

        this.DebugDto.ProcessedMatchCount = (this.DebugDto.ProcessedMatchCount ?? 0) + generatedParts.Count;
        this.DebugDto.ExtractedRawValues.AddRange(generatedParts);

        return generatedParts;
    }

    /// <summary>
    /// 替换逻辑 (Replacer Delegate): 在文本中查找并替换匹配项。
    /// </summary>
    private string Replacer(string inputText)
    {
        var regex = new Regex(this.Config.Pattern, this.BuildRegexOptions(), TimeSpan.FromSeconds(5));

        int foundCount = regex.Count(inputText);
        this.DebugDto.FoundMatchCount += foundCount;

        int replaceCount = this.Config.MaxMatches > 0 ? this.Config.MaxMatches : -1; // -1 表示全部替换
        if (replaceCount > 0)
        {
            this.DebugDto.ProcessedMatchCount = (this.DebugDto.ProcessedMatchCount ?? 0) + Math.Min(foundCount, replaceCount);
        }
        else
        {
            this.DebugDto.ProcessedMatchCount = (this.DebugDto.ProcessedMatchCount ?? 0) + foundCount;
        }

        return regex.Replace(inputText, this.Config.TextOperation.ReplacementTemplate, replaceCount);
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
    internal record RegexParserDebugDto : IRuneProcessorDebugDto
    {
        public string? InputText { get; set; }
        public string? AppliedOptions { get; set; }
        public int FoundMatchCount { get; set; }
        public int? ProcessedMatchCount { get; set; }

        /// <summary>
        /// 用于提取模式
        /// </summary>
        public List<string> ExtractedRawValues { get; set; } = [];

        public object? FinalOutput { get; set; }
        public string? RuntimeError { get; set; }
    }
}

/// <summary>
/// “正则解析”符文的配置。
/// 使用正则表达式，对文本执行提取或替换操作。
/// </summary>
[ClassLabel("正则操作", Icon = "⚙️")]
[RenderWithVueComponent("RegexParserEditor")]
[Display(
    Name = "正则解析",
    Description = "使用正则表达式，对文本执行提取或替换操作。"
)]
[RuneCategory("文本处理")]
internal record RegexParserRuneConfig : AbstractRuneConfig<RegexParserRuneProcessor>
{
    /// <summary>
    /// 通用的文本处理（提取/替换）操作设置。
    /// </summary>
    [Display(Name = "通用操作配置")]
    public TextOperationConfig TextOperation { get; init; } = new()
    {
        // 为正则模式提供更合适的默认值和UI标签
        OperationMode = OperationModeEnum.Extract,
        ReplacementTemplate = "- 角色名: ${name}, 年龄: ${age}岁。"
    };

    /// <summary>
    /// 用于查找匹配项的 .NET 正则表达式，支持命名捕获组。
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    [DataType(DataType.MultilineText)]
    [Display(Name = "正则表达式", Description = ".NET 正则表达式，用于查找所有匹配项。")]
    [DefaultValue(@"姓名：(?<name>\S+)\s+年龄：(?<age>\d+)")]
    public string Pattern { get; init; } = string.Empty;

    // --- 高级选项 ---
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

    #region 静态分析与转换

    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec()
    {
        VarSpecDef consumedDef = this.TextOperation.InputDataType switch
        {
            InputDataTypeEnum.PromptList => CoreVarDefs.PromptList,
            _ => CoreVarDefs.String
        };
        return [new(this.TextOperation.InputVariableName, consumedDef)];
    }

    /// <inheritdoc />
    public override List<ProducedSpec> GetProducedSpec()
    {
        if (this.TextOperation.OperationMode == OperationModeEnum.Replace)
        {
            VarSpecDef producedDef = this.TextOperation.InputDataType switch
            {
                InputDataTypeEnum.PromptList => CoreVarDefs.PromptList,
                _ => CoreVarDefs.String
            };
            return [new ProducedSpec(this.TextOperation.OutputVariableName, producedDef)];
        }

        // 提取模式
        VarSpecDef extractProducedDef = this.TextOperation.ReturnFormat switch
        {
            ReturnFormatEnum.First => CoreVarDefs.String,
            ReturnFormatEnum.AsList => CoreVarDefs.StringList,
            ReturnFormatEnum.AsJsonString => CoreVarDefs.JsonString,
            _ => CoreVarDefs.String
        };
        return [new ProducedSpec(this.TextOperation.OutputVariableName, extractProducedDef)];
    }

    /// <inheritdoc />
    protected override RegexParserRuneProcessor ToCurrentRune(ICreatingContext creatingContext) => new(this,creatingContext);

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