using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Rune;
using YAESandBox.Workflow.Tuum;
using YAESandBox.Workflow.VarSpec;

namespace YAESandBox.Plugin.TextParser.Rune;

/// <summary>
/// “标签解析”符文的运行时处理器。
/// </summary>
public class TagParserRuneProcessor(TagParserRuneConfig config)
    : IProcessorWithDebugDto<TagParserRuneProcessor.TagParserRuneDebugDto>, INormalRune
{
    private TagParserRuneConfig Config { get; } = config;

    /// <inheritdoc />
    public TagParserRuneDebugDto DebugDto { get; } = new();

    /// <summary>
    /// 执行标签解析或替换逻辑。
    /// </summary>
    public async Task<Result> ExecuteAsync(TuumProcessor.TuumProcessorContent tuumProcessorContent,
        CancellationToken cancellationToken = default)
    {
        // 1. 获取输入文本
        object? rawInputValue = tuumProcessorContent.GetTuumVar(this.Config.InputVariableName);
        string inputText = rawInputValue?.ToString() ?? string.Empty;
        this.DebugDto.InputText = inputText;

        if (string.IsNullOrWhiteSpace(inputText))
        {
            // 如果输入为空，则直接设置空输出并成功返回
            tuumProcessorContent.SetTuumVar(this.Config.OutputVariableName,
                this.Config.OperationMode == OperationModeEnum.Replace
                    ? string.Empty // 替换模式输出空字符串
                    : this.FormatOutput([]) // 提取模式使用空列表生成默认输出
            );
            return Result.Ok();
        }

        try
        {
            // 2. 使用 AngleSharp 解析
            var parser = new HtmlParser();
            var document = await parser.ParseDocumentAsync(inputText, cancellationToken);

            // 3. 使用 CSS 选择器查询元素
            // 使用 ToList() 创建一个副本，以避免在迭代时修改集合导致的问题
            var matchedElements = document.QuerySelectorAll(this.Config.Selector).ToList();
            this.DebugDto.MatchedElementCount = matchedElements.Count;

            // 4. 根据操作模式执行不同逻辑
            if (this.Config.OperationMode == OperationModeEnum.Extract)
            {
                // --- 提取模式逻辑 ---
                object finalOutput = this.HandleExtractModeAsync(tuumProcessorContent, matchedElements);
                tuumProcessorContent.SetTuumVar(this.Config.OutputVariableName, finalOutput);
            }
            else // OperationModeEnum.Replace
            {
                // --- 替换模式逻辑 ---
                string finalOutput = this.HandleReplaceModeAsync(tuumProcessorContent, document, matchedElements);
                tuumProcessorContent.SetTuumVar(this.Config.OutputVariableName, finalOutput);
            }

            return Result.Ok();
        }
        catch (Exception ex)
        {
            this.DebugDto.RuntimeError = $"解析失败: {ex.Message}";
            return Result.Fail($"标签解析符文执行失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理提取模式的逻辑。
    /// </summary>
    private object HandleExtractModeAsync(TuumProcessor.TuumProcessorContent tuumProcessorContent, List<IElement> matchedElements)
    {
        var extractedValues = matchedElements.Select(this.MatchContentFromElement).ToList();

        this.DebugDto.ExtractedRawValues = extractedValues;

        object finalOutput = this.FormatOutput(extractedValues);
        this.DebugDto.FinalOutput = finalOutput;

        return finalOutput;
    }

    /// <summary>
    /// 【已重构】处理替换模式的逻辑。
    /// </summary>
    private string HandleReplaceModeAsync(TuumProcessor.TuumProcessorContent tuumProcessorContent, IDocument document,
        List<IElement> matchedElements)
    {
        var replacementDetails = new List<ReplacementDebugInfo>();
        var extractedRawForDebug = new List<string?>();

        foreach (var element in matchedElements)
        {
            // a. 提取用于模板占位符的内容
            string? originalContent = this.MatchContentFromElement(element);
            extractedRawForDebug.Add(originalContent);

            // 记录替换前的状态
            string originalOuterHtml = element.OuterHtml;

            // b. 根据不同的 MatchContentMode 应用替换
            this.ApplyReplacement(element, originalContent);

            // c. 记录替换后的调试信息
            replacementDetails.Add(new ReplacementDebugInfo(
                originalOuterHtml,
                originalContent,
                element.OuterHtml // 获取修改后元素的 OuterHtml
            ));
        }

        // d. 获取整个文档修改后的HTML内容
        string finalModifiedHtml = document.DocumentElement.OuterHtml;

        // e. 更新调试信息和枢机变量
        this.DebugDto.ExtractedRawValues = extractedRawForDebug;
        this.DebugDto.ReplacementDetails = replacementDetails;
        this.DebugDto.FinalOutput = finalModifiedHtml;

        return finalModifiedHtml;
    }

    /// <summary>
    /// 【新增】辅助方法：根据配置，对单个元素执行正确的替换操作。
    /// </summary>
    /// <param name="element">要修改的元素。</param>
    /// <param name="matchedValue">从元素中提取的、用于填充模板的值。</param>
    private void ApplyReplacement(IElement element, string? matchedValue)
    {
        // 使用 ${match} 占位符生成最终要替换成的内容
        string replacementContent =
            this.Config.ReplacementTemplate.Replace("${match}", matchedValue ?? string.Empty, StringComparison.Ordinal);

        // 根据内容目标（MatchContentMode）执行不同的替换策略
        switch (this.Config.MatchContentMode)
        {
            case nameof(MatchContentModeEnum.TextContent):
                // 只替换元素的文本内容，标签结构保持不变
                element.TextContent = replacementContent;
                break;

            case nameof(MatchContentModeEnum.InnerHtml):
                // 替换元素内部的 HTML
                element.InnerHtml = replacementContent;
                break;

            case nameof(MatchContentModeEnum.OuterHtml):
                // 替换整个元素（包括其自身标签）
                element.OuterHtml = replacementContent;
                break;

            case nameof(MatchContentModeEnum.Attribute):
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
            nameof(MatchContentModeEnum.TextContent) => element.TextContent,
            nameof(MatchContentModeEnum.InnerHtml) => element.InnerHtml,
            nameof(MatchContentModeEnum.OuterHtml) => element.OuterHtml,
            nameof(MatchContentModeEnum.Attribute) => !string.IsNullOrEmpty(this.Config.AttributeName)
                ? element.GetAttribute(this.Config.AttributeName) ?? string.Empty
                : string.Empty,
            _ => string.Empty
        };
    }

    /// <summary>
    /// 辅助方法：根据配置格式化输出。
    /// </summary>
    private object FormatOutput(List<string> values)
    {
        return this.Config.ReturnFormat switch
        {
            nameof(ReturnFormatEnum.First) => values.FirstOrDefault() ?? string.Empty,
            nameof(ReturnFormatEnum.AsList) => values,
            nameof(ReturnFormatEnum.AsJsonString) => JsonSerializer.Serialize(values, YaeSandBoxJsonHelper.JsonSerializerOptions),
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
public record TagParserRuneConfig : AbstractRuneConfig<TagParserRuneProcessor>
{
    #region 配置项

    /// <summary>
    /// 指定从哪个枢机变量中读取要解析的原始HTML/XML文本。
    /// </summary>
    [Required]
    [Display(Name = "输入变量名", Description = "包含标签文本的源变量。")]
    public required string InputVariableName { get; init; }

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
    /// 一个标准的CSS选择器，用于定位目标元素。
    /// </summary>
    [Required]
    [DataType(DataType.MultilineText)]
    [Display(Name = "CSS 选择器", Description = "使用CSS选择器语法来定位一个或多个元素。")]
    [DefaultValue("div.item")]
    public required string Selector { get; init; }

    /// <summary>
    /// 定义要对匹配元素的哪个部分进行操作。
    /// </summary>
    [Required]
    [Display(Name = "内容目标", Description = "定义要对匹配元素的哪个部分进行操作（提取或作为替换模板的输入）。")]
    [DefaultValue(MatchContentModeEnum.TextContent)]
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
    public required string MatchContentMode { get; init; }

    /// <summary>
    /// 当“提取模式”为“提取属性”时，指定要提取的属性名称。
    /// </summary>
    [Display(Name = "属性名", Description = "当提取模式为“提取属性”时，填写此项。例如 'src', 'href'。")]
    public string? AttributeName { get; init; }

    // --- 替换模式专属 ---
    /// <summary>
    /// 替换模板，用于生成替换后的内容。
    /// </summary>
    [Display(Name = "替换模板", Description = "【替换模式】下生效。使用 ${match} 占位符代表由“内容目标”定义匹配到的原始内容。")]
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
    [DefaultValue(nameof(ReturnFormatEnum.First))]
    public required string ReturnFormat { get; init; }

    /// <summary>
    /// 指定将提取出的结果存入哪个枢机变量。
    /// </summary>
    [Required]
    [Display(Name = "输出变量名", Description = "用于存储提取结果的目标变量。")]
    public required string OutputVariableName { get; init; }

    #endregion

    #region 静态分析与转换

    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec() => [new(this.InputVariableName, CoreVarDefs.String)];

    /// <inheritdoc />
    public override List<ProducedSpec> GetProducedSpec()
    {
        if (this.OperationMode == OperationModeEnum.Replace)
            return [new ProducedSpec(this.OutputVariableName, CoreVarDefs.String)];

        // 根据 ReturnFormat 决定输出变量的类型定义
        var producedDef = this.ReturnFormat switch
        {
            nameof(ReturnFormatEnum.First) => CoreVarDefs.String,
            nameof(ReturnFormatEnum.AsList) => CoreVarDefs.StringList,
            nameof(ReturnFormatEnum.AsJsonString) => CoreVarDefs.String,
            _ => CoreVarDefs.String // 默认或错误情况
        };

        return
        [
            new ProducedSpec(this.OutputVariableName, producedDef)
        ];
    }

    /// <inheritdoc />
    protected override TagParserRuneProcessor ToCurrentRune(WorkflowRuntimeService workflowRuntimeService) => new(this);

    #endregion
}

// 为了清晰，我们把枚举定义在同一个文件里
public enum MatchContentModeEnum
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