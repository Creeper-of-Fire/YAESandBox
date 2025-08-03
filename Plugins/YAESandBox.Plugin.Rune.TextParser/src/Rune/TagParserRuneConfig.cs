using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Rune;
using YAESandBox.Workflow.Tuum;

namespace YAESandBox.Plugin.TextParser.Rune;

/// <summary>
/// “标签解析”符文的运行时处理器。
/// </summary>
public class TagParserRuneProcessor(TagParserRuneConfig config)
    : IWithDebugDto<TagParserRuneProcessor.TagParserRuneDebugDto>, INormalRune
{
    private TagParserRuneConfig Config { get; } = config;

    /// <inheritdoc />
    public TagParserRuneDebugDto DebugDto { get; } = new();

    /// <summary>
    /// 执行标签解析逻辑。
    /// </summary>
    public Task<Result> ExecuteAsync(TuumProcessor.TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
    {
        // 1. 获取输入文本
        object? rawInputValue = tuumProcessorContent.GetTuumVar(this.Config.InputVariableName);
        string inputText = rawInputValue?.ToString() ?? string.Empty;
        this.DebugDto.InputText = inputText;

        if (string.IsNullOrWhiteSpace(inputText))
        {
            // 如果输入为空，则直接设置空输出并成功返回
            tuumProcessorContent.SetTuumVar(this.Config.OutputVariableName, null);
            return Task.FromResult(Result.Ok());
        }

        try
        {
            // 2. 使用 AngleSharp 解析
            var parser = new HtmlParser();
            var document = parser.ParseDocument(inputText);

            // 3. 使用 CSS 选择器查询元素
            var matchedElements = document.QuerySelectorAll(this.Config.Selector);
            this.DebugDto.MatchedElementCount = matchedElements.Length;

            // 4. 根据配置提取内容
            var extractedValues = new List<string?>();
            foreach (var element in matchedElements)
            {
                string? value = this.ExtractValueFromElement(element);
                extractedValues.Add(value);
            }

            this.DebugDto.ExtractedRawValues = extractedValues;

            // 5. 根据返回格式组装最终结果
            object? finalOutput = this.FormatOutput(extractedValues);
            this.DebugDto.FinalOutput = finalOutput;

            // 6. 将结果写入祝祷上下文
            tuumProcessorContent.SetTuumVar(this.Config.OutputVariableName, finalOutput);

            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            this.DebugDto.RuntimeError = $"解析失败: {ex.Message}";
            return Task.FromResult(Result.Fail($"标签解析符文执行失败: {ex.Message}").ToResult());
        }
    }

    /// <summary>
    /// 辅助方法：从单个元素中提取值。
    /// </summary>
    private string? ExtractValueFromElement(IElement element)
    {
        return this.Config.ExtractionMode switch
        {
            nameof(ExtractionModeEnum.TextContent) => element.TextContent,
            nameof(ExtractionModeEnum.InnerHtml) => element.InnerHtml,
            nameof(ExtractionModeEnum.OuterHtml) => element.OuterHtml,
            nameof(ExtractionModeEnum.Attribute) => !string.IsNullOrEmpty(this.Config.AttributeName)
                ? element.GetAttribute(this.Config.AttributeName)
                : null,
            _ => null
        };
    }

    /// <summary>
    /// 辅助方法：根据配置格式化输出。
    /// </summary>
    private object? FormatOutput(List<string?> values)
    {
        return this.Config.ReturnFormat switch
        {
            nameof(ReturnFormatEnum.First) => values.FirstOrDefault(),
            nameof(ReturnFormatEnum.AsList) => values,
            nameof(ReturnFormatEnum.AsJsonString) => JsonSerializer.Serialize(values, YaeSandBoxJsonHelper.JsonSerializerOptions),
            _ => null
        };
    }

    /// <summary>
    /// 标签解析符文的调试 DTO。
    /// </summary>
    public record TagParserRuneDebugDto : IRuneProcessorDebugDto
    {
        public string? InputText { get; set; }
        public int MatchedElementCount { get; set; }
        public List<string?> ExtractedRawValues { get; set; } = [];
        public object? FinalOutput { get; set; }
        public string? RuntimeError { get; set; }
    }
}

/// <summary>
/// “标签解析”符文的配置。
/// 使用CSS选择器从HTML/XML文本中精确提取数据。
/// </summary>
[ClassLabel("🏷️标签解析")]
[RenderWithVueComponent("TagParserEditor")]
public record TagParserRuneConfig : AbstractRuneConfig<TagParserRuneProcessor>
{
    #region 配置项

    /// <summary>
    /// 指定从哪个祝祷变量中读取要解析的原始HTML/XML文本。
    /// </summary>
    [Required]
    [Display(Name = "输入变量名", Description = "包含标签文本的源变量。")]
    public required string InputVariableName { get; init; }

    /// <summary>
    /// 一个标准的CSS选择器，用于定位目标元素。
    /// </summary>
    [Required]
    [DataType(DataType.MultilineText)]
    [Display(Name = "CSS 选择器", Description = "使用CSS选择器语法来定位一个或多个元素。")]
    [DefaultValue("div.item")]
    public required string Selector { get; init; }

    /// <summary>
    /// 定义如何从匹配的元素中提取数据。
    /// </summary>
    [Required]
    [Display(Name = "提取模式", Description = "决定要从找到的元素中获取什么内容。")]
    [StringOptions(
        [
            nameof(ExtractionModeEnum.TextContent),
            nameof(ExtractionModeEnum.InnerHtml),
            nameof(ExtractionModeEnum.OuterHtml),
            nameof(ExtractionModeEnum.Attribute)
        ],
        [
            "纯文本",
            "内部HTML",
            "完整HTML",
            "提取属性"
        ]
    )]
    [DefaultValue(nameof(ExtractionModeEnum.TextContent))]
    public required string ExtractionMode { get; init; }

    /// <summary>
    /// 当“提取模式”为“提取属性”时，指定要提取的属性名称。
    /// </summary>
    [Display(Name = "属性名", Description = "当提取模式为“提取属性”时，填写此项。例如 'src', 'href'。")]
    public string? AttributeName { get; init; }

    /// <summary>
    /// 如果选择器匹配到多个元素，定义如何格式化返回结果。
    /// </summary>
    [Required]
    [Display(Name = "返回格式", Description = "定义当匹配到多个元素时的输出形式。")]
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
    [DefaultValue(nameof(ReturnFormatEnum.First))]
    public required string ReturnFormat { get; init; }

    /// <summary>
    /// 指定将提取出的结果存入哪个祝祷变量。
    /// </summary>
    [Required]
    [Display(Name = "输出变量名", Description = "用于存储提取结果的目标变量。")]
    public required string OutputVariableName { get; init; }

    #endregion

    #region 静态分析与转换

    /// <inheritdoc />
    public override List<string> GetConsumedVariables() => [this.InputVariableName];

    /// <inheritdoc />
    public override List<string> GetProducedVariables() => [this.OutputVariableName];

    /// <inheritdoc />
    protected override TagParserRuneProcessor ToCurrentRune(WorkflowRuntimeService workflowRuntimeService) => new(this);

    #endregion
}

// 为了清晰，我们把枚举定义在同一个文件里
public enum ExtractionModeEnum
{
    TextContent,
    InnerHtml,
    OuterHtml,
    Attribute
}

public enum ReturnFormatEnum
{
    First,
    AsList,
    AsJsonString
}