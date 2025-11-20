using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.Core.Config.RuneConfig;
using YAESandBox.Workflow.Core.DebugDto;
using YAESandBox.Workflow.Core.Runtime.Processor;
using YAESandBox.Workflow.Core.Runtime.Processor.RuneProcessor;
using YAESandBox.Workflow.Core.VarSpec;
using YAESandBox.Workflow.Schema;
using static YAESandBox.Workflow.Core.Runtime.Processor.TuumProcessor;

namespace YAESandBox.Workflow.ExactRune;

/// <summary>
/// “文本模板”符文的运行时处理器。
/// 根据模板和上下文变量，生成一个最终的文本字符串。
/// </summary>
internal class TextTemplateRuneProcessor(TextTemplateRuneConfig config, ICreatingContext creatingContext)
    : NormalRuneProcessor<TextTemplateRuneConfig, TextTemplateRuneProcessor.TextTemplateRuneDebugDto>(config, creatingContext)
{
    public override TextTemplateRuneDebugDto DebugDto { get; } = new()
    {
        OriginalTemplate = config.Template,
    };

    public override Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. 使用与 PromptGenerationRune 相同的逻辑替换占位符
            string finalContent = Helpers.StringTemplateHelper.Render(
                this.Config.Template,
                tuumProcessorContent,
                this.DebugDto.ResolvedPlaceholders,
                this.DebugDto.UnresolvedPlaceholders
                // 此 Rune 的 DebugDto 没有专门的 log 列表，所以这里不传 logAction，或者可以扩展 DebugDto
            );
            this.DebugDto.FinalContent = finalContent;

            // 2. 将生成的内容设置到指定的输出变量中
            tuumProcessorContent.SetTuumVar(this.Config.OutputVariableName, finalContent);

            return Result.Ok().AsCompletedTask();
        }
        catch (Exception ex)
        {
            var error = new Error("文本模板组装失败。", ex);
            this.DebugDto.RuntimeError = error.ToDetailString();
            return Result.Fail(error).AsCompletedTask();
        }
    }

    internal record TextTemplateRuneDebugDto : IRuneProcessorDebugDto
    {
        public string OriginalTemplate { get; init; } = string.Empty;
        public string? FinalContent { get; set; }
        public Dictionary<string, string> ResolvedPlaceholders { get; } = [];
        public List<string> UnresolvedPlaceholders { get; } = [];
        public string? RuntimeError { get; set; }
    }
}

/// <summary>
/// “文本模板”符文的配置。
/// </summary>
[ClassLabel("文本模板", Icon = "📄")]
[RuneCategory("文本处理")]
internal partial record TextTemplateRuneConfig : AbstractRuneConfig<TextTemplateRuneProcessor>
{
    #region Config Properties

    [Required]
    [Display(Name = "输出变量名", Description = "用于存储组装后文本的目标变量名。")]
    public string OutputVariableName { get; init; } = "assembledText";

    [Required(AllowEmptyStrings = true)]
    [DataType(DataType.MultilineText)]
    [Display(
        Name = "文本模板",
        Description = "编写包含动态占位符（例如 `{{variable}}` 或 `{{player.name}}`）的文本模板。",
        Prompt = "例如：'你好，{{player.name}}！你的等级是{{player.level}}。'"
    )]
    [DefaultValue("")]
    public string Template { get; init; } = "";

    #endregion

    #region Static Analysis

    // 静态分析逻辑与 PromptGenerationRuneConfig 完全相同，用于推断消费的变量
    public override List<ConsumedSpec> GetConsumedSpec()
    {
        // 提取占位符
        var placeholders = Helpers.StringTemplateHelper.ExtractPlaceholders(this.Template);

        // 使用通用逻辑推断变量结构
        return Helpers.StringTemplateHelper.InferConsumedSpecs(placeholders);
    }

    public override List<ProducedSpec> GetProducedSpec() => [new(this.OutputVariableName, CoreVarDefs.String)];

    #endregion

    protected override TextTemplateRuneProcessor ToCurrentRune(ICreatingContext creatingContext) => new(this, creatingContext);
}