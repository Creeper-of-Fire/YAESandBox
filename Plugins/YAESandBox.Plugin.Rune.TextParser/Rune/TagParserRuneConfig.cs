using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Rune;
using YAESandBox.Workflow.Tuum;
using YAESandBox.Workflow.VarSpec;
using static YAESandBox.Plugin.Rune.TextParser.Rune.TagParserRuneProcessor;

namespace YAESandBox.Plugin.Rune.TextParser.Rune;

/// <summary>
/// “标签解析”符文的运行时处理器。
/// </summary>
public class TagParserRuneProcessor(TagParserRuneConfig config)
    : INormalRune<TagParserRuneConfig, TagParserRuneDebugDto>
{
    /// <inheritdoc />
    public TagParserRuneConfig Config { get; } = config;

    /// <inheritdoc />
    public TagParserRuneDebugDto DebugDto { get; } = new();

    /// <summary>
    /// 执行标签解析或替换逻辑。
    /// </summary>
    public Task<Result> ExecuteAsync(TuumProcessor.TuumProcessorContent tuumProcessorContent,
        CancellationToken cancellationToken = default)
    {
        // 获取输入文本
        object? rawInputValue = tuumProcessorContent.GetTuumVar(this.Config.TextOperation.InputVariableName);

        try
        {
            TextProcessingInput input;
            if (this.Config.TextOperation.InputDataType == InputDataTypeEnum.PromptList && rawInputValue is List<RoledPromptDto> prompts)
            {
                input = TextProcessingInput.FromPromptList(prompts);
                // 为了调试方便，将列表序列化为JSON字符串
                this.DebugDto.InputText = JsonSerializer.Serialize(prompts, YaeSandBoxJsonHelper.JsonSerializerOptions);
            }
            else
            {
                string inputText = rawInputValue?.ToString() ?? string.Empty;
                input = TextProcessingInput.FromString(inputText);
                this.DebugDto.InputText = inputText;
            }

            // 调用通用辅助类来处理
            object finalOutput = TextOperationHelper.Process(
                input,
                this.Config.TextOperation.OperationMode,
                this.Extractor,
                this.Replacer,
                this.Config.TextOperation.ReturnFormat
            );

            // 在提取模式下，调试信息中的 FinalOutput 需要在这里更新
            if (this.Config.TextOperation.OperationMode == OperationModeEnum.Extract)
            {
                this.DebugDto.FinalOutput = finalOutput;
            }

            // 设置输出变量
            tuumProcessorContent.SetTuumVar(this.Config.TextOperation.OutputVariableName, finalOutput);

            return Result.Ok().AsCompletedTask();
        }
        catch (Exception ex)
        {
            this.DebugDto.RuntimeError = $"解析失败。{ex.ToFormattedString()}";
            return Result.Fail("标签解析符文执行失败。", ex).AsCompletedTask();
        }
    }

    // 替换逻辑：接收文本，返回完整替换后的文本
    string Replacer(string text)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument(text);
        var matchedElements = document.QuerySelectorAll(this.Config.Selector).ToList();
        this.DebugDto.MatchedElementCount += matchedElements.Count; // 使用 += 以便在PromptList模式下累加

        var replacementDetails = this.DebugDto.ReplacementDetails ??= [];
        var extractedRawForDebug = this.DebugDto.ExtractedRawValues;

        foreach (var element in matchedElements)
        {
            string originalContent = this.MatchContentFromElement(element);
            extractedRawForDebug.Add(originalContent);
            string originalOuterHtml = element.OuterHtml;

            this.ApplyReplacement(element, originalContent);

            replacementDetails.Add(new ReplacementDebugInfo(originalOuterHtml, originalContent, element.OuterHtml));
        }

        string finalModifiedHtml = document.DocumentElement.OuterHtml;

        // 注意：在PromptList模式下，这个FinalOutput会被多次覆盖，最终只显示最后一个prompt的处理结果。
        // 但由于核心调试信息（如MatchedElementCount, ReplacementDetails）是累加的，所以总体调试信息仍然有效。
        this.DebugDto.FinalOutput = finalModifiedHtml;
        return finalModifiedHtml;
    }

    // 提取逻辑：接收文本，返回匹配内容的字符串列表
    List<string> Extractor(string text)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument(text);
        var matchedElements = document.QuerySelectorAll(this.Config.Selector).ToList();
        this.DebugDto.MatchedElementCount += matchedElements.Count; // 使用 += 以便在PromptList模式下累加

        var extractedValues = matchedElements.Select(this.MatchContentFromElement).ToList();
        this.DebugDto.ExtractedRawValues.AddRange(extractedValues);
        return extractedValues;
    }

    /// <summary>
    /// 辅助方法：根据配置，对单个元素执行正确的替换操作。
    /// </summary>
    /// <param name="element">要修改的元素。</param>
    /// <param name="matchedValue">从元素中提取的、用于填充模板的值。</param>
    private void ApplyReplacement(IElement element, string? matchedValue)
    {
        // 使用 ${match} 占位符生成最终要替换成的内容
        string replacementContent =
            this.Config.TextOperation.ReplacementTemplate.Replace("${match}", matchedValue ?? string.Empty,
                StringComparison.OrdinalIgnoreCase);

        // 根据内容目标（MatchContentMode）执行不同的替换策略
        switch (this.Config.MatchContentMode)
        {
            case MatchContentModeEnum.TextContent:
                // 只替换元素的文本内容，标签结构保持不变
                element.TextContent = replacementContent;
                break;

            case MatchContentModeEnum.InnerHtml:
                // 替换元素内部的 HTML
                element.InnerHtml = replacementContent;
                break;

            case MatchContentModeEnum.OuterHtml:
                // 替换整个元素（包括其自身标签）
                element.OuterHtml = replacementContent;
                break;

            case MatchContentModeEnum.Attribute:
                // 只替换指定属性的值
                if (!string.IsNullOrEmpty(this.Config.AttributeName))
                {
                    element.SetAttribute(this.Config.AttributeName, replacementContent);
                }

                // 如果属性名为空，则不执行任何操作
                break;
        }
    }


    /// <summary>
    /// 辅助方法：从单个元素中提取值。
    /// </summary>
    private string MatchContentFromElement(IElement element)
    {
        return this.Config.MatchContentMode switch
        {
            MatchContentModeEnum.TextContent => element.TextContent,
            MatchContentModeEnum.InnerHtml => element.InnerHtml,
            MatchContentModeEnum.OuterHtml => element.OuterHtml,
            MatchContentModeEnum.Attribute => !string.IsNullOrEmpty(this.Config.AttributeName)
                ? element.GetAttribute(this.Config.AttributeName) ?? string.Empty
                : string.Empty,
            _ => string.Empty
        };
    }

    /// <summary>
    /// 替换操作的单条调试信息记录。
    /// </summary>
    public record ReplacementDebugInfo(string OriginalOuterHtml, string? MatchedValue, string NewOuterHtml);

    /// <summary>
    /// 标签解析符文的调试 DTO。
    /// </summary>
    public record TagParserRuneDebugDto : IRuneProcessorDebugDto
    {
        public string? InputText { get; set; }
        public int MatchedElementCount { get; set; }
        public List<string> ExtractedRawValues { get; set; } = [];
        public object? FinalOutput { get; set; }
        public string? RuntimeError { get; set; }

        /// <summary>
        /// 在替换模式下，记录详细的替换过程。
        /// </summary>
        public List<ReplacementDebugInfo>? ReplacementDetails { get; set; }
    }
}

/// <summary>
/// “标签解析”符文的配置。
/// 使用CSS选择器从HTML/XML文本中精确提取数据。
/// </summary>
[ClassLabel("🏷️标签解析")]
[RenderWithVueComponent("TagParserEditor")]
[Display(
    Name = "标签解析",
    Description = "使用CSS选择器从HTML/XML文本中精确提取数据。"
)]
public record TagParserRuneConfig : AbstractRuneConfig<TagParserRuneProcessor>
{
    #region 配置项

    /// <summary>
    /// 通用的文本处理（提取/替换）操作设置。
    /// </summary>
    [Display(Name = "通用操作配置")]
    public TextOperationConfig TextOperation { get; init; } = new();

    // --- 仅与标签解析逻辑相关的配置 ---

    /// <summary>
    /// 一个标准的CSS选择器，用于定位目标元素。
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    [DataType(DataType.MultilineText)]
    [Display(Name = "CSS 选择器", Description = "使用CSS选择器语法来定位一个或多个元素。")]
    [DefaultValue("div.item")]
    public string Selector { get; init; } = "div.item";

    /// <summary>
    /// 定义要对匹配元素的哪个部分进行操作。
    /// </summary>
    [Required]
    [Display(Name = "内容目标", Description = "定义要对匹配元素的哪个部分进行操作（提取或作为替换模板的输入）。")]
    [DefaultValue(nameof(MatchContentModeEnum.TextContent))]
    [StringOptions(
        [
            nameof(MatchContentModeEnum.TextContent),
            nameof(MatchContentModeEnum.InnerHtml),
            nameof(MatchContentModeEnum.OuterHtml),
            nameof(MatchContentModeEnum.Attribute)
        ],
        [
            "纯文本",
            "内部HTML",
            "完整HTML",
            "提取属性"
        ]
    )]
    public MatchContentModeEnum MatchContentMode { get; init; } = MatchContentModeEnum.TextContent;

    /// <summary>
    /// 当“提取模式”为“提取属性”时，指定要提取的属性名称。
    /// </summary>
    [Display(Name = "属性名", Description = "当提取模式为“提取属性”时，填写此项。例如 'src', 'href'。")]
    public string? AttributeName { get; init; }

    #endregion

    #region 静态分析与转换

    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec()
    {
        // 根据用户选择的输入类型，声明正确的消费变量类型
        var consumedDef = this.TextOperation.InputDataType switch
        {
            InputDataTypeEnum.PromptList => CoreVarDefs.PromptList,
            _ => CoreVarDefs.String
        };
        return [new ConsumedSpec(this.TextOperation.InputVariableName, consumedDef)];
    }

    /// <inheritdoc />
    public override List<ProducedSpec> GetProducedSpec()
    {
        // 替换模式下，输出类型与输入类型保持一致
        if (this.TextOperation.OperationMode == OperationModeEnum.Replace)
        {
            var producedDef = this.TextOperation.InputDataType switch
            {
                InputDataTypeEnum.PromptList => CoreVarDefs.PromptList,
                _ => CoreVarDefs.String
            };
            return [new ProducedSpec(this.TextOperation.OutputVariableName, producedDef)];
        }

        // 提取模式下，输出类型由 ReturnFormat 决定，与输入类型无关
        var extractProducedDef = this.TextOperation.ReturnFormat switch
        {
            ReturnFormatEnum.First => CoreVarDefs.String,
            ReturnFormatEnum.AsList => CoreVarDefs.StringList,
            ReturnFormatEnum.AsJsonString => CoreVarDefs.JsonString,
            _ => CoreVarDefs.String // 默认或错误情况
        };

        return [new ProducedSpec(this.TextOperation.OutputVariableName, extractProducedDef)];
    }

    /// <inheritdoc />
    protected override TagParserRuneProcessor ToCurrentRune(WorkflowRuntimeService workflowRuntimeService) => new(this);

    #endregion
}

/// <summary>
/// 定义符文处理时，从匹配元素中提取的内容
/// </summary>
public enum MatchContentModeEnum
{
    TextContent,
    InnerHtml,
    OuterHtml,
    Attribute
}