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
            string finalContent = this.SubstitutePlaceholders(this.Config.Template, tuumProcessorContent);
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

    /// <summary>
    /// 使用 Tuum 上下文中的变量替换模板中的占位符，支持点符号访问。
    /// </summary>
    private string SubstitutePlaceholders(string template, TuumProcessorContent tuumContent)
    {
        return TextTemplateRuneConfig.PlaceholderRegex().Replace(template, match =>
        {
            // 例如: 'player.name'
            string path = match.Groups["path"].Value;

            // 使用 TuumContent 提供的路径解析方法
            if (tuumContent.TryGetTuumVarByPath<object>(path, out object? value))
            {
                string stringValue = value.ToString() ?? string.Empty;
                this.DebugDto.ResolvedPlaceholders[path] = stringValue;
                return stringValue;
            }

            // 未找到或值为 null
            this.DebugDto.UnresolvedPlaceholders.Add(path);
            return string.Empty; // 替换为空字符串
        });
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
    // 正则表达式与 PromptGenerationRuneConfig 保持一致，但捕获组命名为 'path' 以提高可读性
    [GeneratedRegex(@"\{\{(?<path>[^}]+?)\}\}")]
    internal static partial Regex PlaceholderRegex();

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
        var rootSpecs = new Dictionary<string, VarSpecDef>();
        var allPlaceholders = PlaceholderRegex().Matches(this.Template)
            .Select(m => m.Groups["path"].Value.Trim());

        foreach (string path in allPlaceholders)
        {
            string[] parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            string rootVarName = parts[0];

            if (parts.Length == 1)
            {
                if (!rootSpecs.ContainsKey(rootVarName))
                {
                    rootSpecs[rootVarName] = CoreVarDefs.Any with { Description = "可被ToString的任意类型。" };
                }

                continue;
            }

            if (!rootSpecs.TryGetValue(rootVarName, out var currentDef) || currentDef is not RecordVarSpecDef)
            {
                currentDef = CoreVarDefs.RecordStringAny with
                {
                    Properties = new Dictionary<string, VarSpecDef>(),
                    Description = $"根据模板为变量'{rootVarName}'推断出的数据结构。"
                };
                rootSpecs[rootVarName] = currentDef;
            }

            var currentRecord = (RecordVarSpecDef)currentDef;

            for (int i = 1; i < parts.Length; i++)
            {
                string propName = parts[i];

                if (i == parts.Length - 1)
                {
                    if (!currentRecord.Properties.ContainsKey(propName))
                    {
                        currentRecord.Properties[propName] = CoreVarDefs.Any with { Description = "可被ToString的任意类型。" };
                    }

                    break;
                }

                if (!currentRecord.Properties.TryGetValue(propName, out var nextDef) || nextDef is not RecordVarSpecDef)
                {
                    nextDef = CoreVarDefs.RecordStringAny with
                    {
                        Properties = new Dictionary<string, VarSpecDef>(),
                        Description = $"为'{propName}'推断出的嵌套数据结构。"
                    };
                    currentRecord.Properties[propName] = nextDef;
                }

                currentRecord = (RecordVarSpecDef)nextDef;
            }
        }

        return rootSpecs.Select(kvp => new ConsumedSpec(kvp.Key, kvp.Value)).ToList();
    }

    public override List<ProducedSpec> GetProducedSpec() => [new(this.OutputVariableName, CoreVarDefs.String)];

    #endregion

    protected override TextTemplateRuneProcessor ToCurrentRune(ICreatingContext creatingContext) => new(this, creatingContext);
}