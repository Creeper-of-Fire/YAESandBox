using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Tomlyn;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Config.RuneConfig;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.ExactRune.Helpers;
using YAESandBox.Workflow.Runtime.Processor;
using YAESandBox.Workflow.Runtime.Processor.RuneProcessor;
using YAESandBox.Workflow.VarSpec;
using static YAESandBox.Workflow.Runtime.Processor.TuumProcessor;

namespace YAESandBox.Workflow.ExactRune;

/// <summary>
/// “模板解析”符文的运行时处理器。
/// </summary>
internal class TemplateParserRuneProcessor(TemplateParserRuneConfig config, ICreatingContext creatingContext)
    : NormalRuneProcessor<TemplateParserRuneConfig, TemplateParserRuneProcessor.TemplateParserRuneDebugDto>(config, creatingContext)
{
    private static readonly Regex PlaceholderRegex = new(@"\$\{(?<name>\w+)(?::\w+)?\}", RegexOptions.Compiled);

    public override Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
    {
        // 1. 获取输入并初始化
        var inputText = tuumProcessorContent.GetTuumVar<string>(this.Config.InputVariableName) ?? string.Empty;
        this.DebugDto.InputText = inputText;
        var capturedValues = new Dictionary<string, string>();
        var regexOptions = this.BuildRegexOptions();

        try
        {
            // 2. 聚合所有命名捕获组
            foreach (var pattern in this.Config.ExtractionPatterns)
            {
                if (string.IsNullOrWhiteSpace(pattern)) continue;

                var regex = new Regex(pattern, regexOptions, TimeSpan.FromSeconds(5));
                var match = regex.Match(inputText);

                if (match.Success)
                {
                    foreach (var groupName in regex.GetGroupNames())
                    {
                        if (int.TryParse(groupName, out _)) continue; // 跳过数字命名的组
                        capturedValues[groupName] = match.Groups[groupName].Value;
                    }
                }
            }

            this.DebugDto.CapturedValues = capturedValues;

            // 3. 填充 TOML 模板
            var filledTemplate = PlaceholderRegex.Replace(this.Config.OutputTemplate, match =>
                capturedValues.TryGetValue(match.Groups["name"].Value, out var value) ? value : string.Empty
            );
            this.DebugDto.FilledTomlTemplate = filledTemplate;

            // 4. 解析填充后的 TOML 并转换为运行时对象
            var model = Toml.ToModel(filledTemplate);
            var runtimeValue = TomlRuneHelper.ConvertTomlObjectToRuntimeValue(model);

            // TOML 的根总是一个字典
            if (runtimeValue is Dictionary<string, object?> outputDict)
            {
                this.DebugDto.FinalOutput = outputDict;
                tuumProcessorContent.SetTuumVar(this.Config.OutputVariableName, outputDict);
            }
            else
            {
                // 理论上不应发生，因为 TOML 根总是表
                throw new InvalidOperationException("解析后的 TOML 根对象不是一个有效的字典。");
            }

            return Result.Ok().AsCompletedTask();
        }
        catch (Exception ex)
        {
            var error = new Error("模板解析执行失败。", ex);
            this.DebugDto.RuntimeError = error.ToDetailString();
            return Result.Fail(error).AsCompletedTask();
        }
    }

    private RegexOptions BuildRegexOptions()
    {
        var options = RegexOptions.None;
        if (this.Config.IgnoreCase) options |= RegexOptions.IgnoreCase;
        if (this.Config.Multiline) options |= RegexOptions.Multiline;
        if (this.Config.DotAll) options |= RegexOptions.Singleline;
        options |= RegexOptions.Compiled;
        return options;
    }

    internal record TemplateParserRuneDebugDto : IRuneProcessorDebugDto
    {
        public string? InputText { get; set; }
        public Dictionary<string, string>? CapturedValues { get; set; }
        public string? FilledTomlTemplate { get; set; }
        public Dictionary<string, object?>? FinalOutput { get; set; }
        public string? RuntimeError { get; set; }
    }
}

/// <summary>
/// “模板解析”符文的配置。
/// 使用正则表达式的命名捕获组和TOML模板，从文本中提取并构建结构化数据。
/// </summary>
[ClassLabel("🛠️模板解析")]
[RuneCategory("文本解析")] 
internal partial record TemplateParserRuneConfig : AbstractRuneConfig<TemplateParserRuneProcessor>
{
    private const string GroupIO = "输入/输出";
    private const string GroupRegex = "全局正则选项";
    private static readonly Regex PlaceholderForSpecRegex = GetPlaceholderForSpecRegex();

    [GeneratedRegex(@"\$\{(?<name>\w+)(?::(?<type>\w+))?\}", RegexOptions.Compiled)]
    private static partial Regex GetPlaceholderForSpecRegex();

    #region Config Properties

    [Required]
    [InlineGroup(GroupIO)]
    [Display(Name = "输入变量名")]
    public string InputVariableName { get; init; } = "AiOutput";

    [Required]
    [InlineGroup(GroupIO)]
    [Display(Name = "输出变量名", Description = "用于存储解析结果（一个对象）的目标变量名。")]
    public string OutputVariableName { get; init; } = "parsedResult";

    [InlineGroup(GroupRegex)]
    [DefaultValue(true)]
    [Display(Name = "忽略大小写 (i)")]
    public bool IgnoreCase { get; init; } = true;

    [InlineGroup(GroupRegex)]
    [DefaultValue(true)]
    [Display(Name = "多行模式 (m)")]
    public bool Multiline { get; init; } = true;

    [InlineGroup(GroupRegex)]
    [DefaultValue(true)]
    [Display(Name = "点号匹配所有 (s)")]
    public bool DotAll { get; init; } = true;

    [Display(Name = "提取模式", Description = "定义一个或多个正则表达式，用于从输入文本中捕获命名组。后匹配到的同名组会覆盖前者。")]
    public List<string> ExtractionPatterns { get; init; } = [];

    [Required(AllowEmptyStrings = true)]
    [DataType(DataType.MultilineText)]
    [RenderWithMonacoEditor("toml")]
    [Display(Name = "输出 TOML 模板",
        Description = "使用 TOML 定义输出对象的结构。使用 ${group_name} 引用捕获组的值。使用 ${group_name:type} (如 :int, :float, :bool) 可为类型推断提供精确提示。")]
    public string OutputTemplate { get; init; } =
        """
        # 示例：
        # name = "${character_name}"
        # level = ${level:int}
        # enabled = ${is_enabled:bool}
        #
        # [stats]
        # "攻击" = ${atk:int}
        # defense = ${def:int}
        """;

    #endregion

    #region Static Analysis

    public override List<ConsumedSpec> GetConsumedSpec() => [new(this.InputVariableName, CoreVarDefs.String)];

    public override List<ProducedSpec> GetProducedSpec()
    {
        if (string.IsNullOrWhiteSpace(this.OutputTemplate))
        {
            return [];
        }

        try
        {
            // 1. 预处理 TOML 模板，用默认值替换占位符，使其成为合法的 TOML
            var preprocessedTemplate = PreprocessTomlTemplateForSpec(this.OutputTemplate);

            // 2. 解析预处理后的模板
            var model = Toml.ToModel(preprocessedTemplate);

            // 3. 将 TOML 模型转换为 VarSpecDef
            // 我们的输出变量本身就是这个顶层对象
            var varDef = TomlRuneHelper.ConvertTomlObjectToVarSpecDef(model);

            return [new ProducedSpec(this.OutputVariableName, varDef)];
        }
        catch
        {
            // 如果模板格式错误导致解析失败，则无法推断类型
            return [];
        }
    }

    private static string PreprocessTomlTemplateForSpec(string template)
    {
        return PlaceholderForSpecRegex.Replace(template, match =>
        {
            var typeHint = match.Groups["type"].Value;
            return typeHint switch
            {
                "int" => "0",
                "float" => "0.0",
                "bool" => "true",
                _ => "\"\"" // 默认为字符串
            };
        });
    }

    #endregion

    protected override TemplateParserRuneProcessor ToCurrentRune(ICreatingContext creatingContext) => new(this, creatingContext);
}