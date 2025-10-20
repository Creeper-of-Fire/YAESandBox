using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using Tomlyn;
using Tomlyn.Model;
using YAESandBox.Depend.Logger;
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
internal partial class TemplateParserRuneProcessor(TemplateParserRuneConfig config, ICreatingContext creatingContext)
    : NormalRuneProcessor<TemplateParserRuneConfig, TemplateParserRuneProcessor.TemplateParserRuneDebugDto>(config, creatingContext)
{
    private static readonly Regex PlaceholderRegex = GetPlaceholderRegex();

    [GeneratedRegex(@"\$\{(?<name>\w+)(?::\w+)?\}", RegexOptions.Compiled)]
    private static partial Regex GetPlaceholderRegex();

    public override Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
    {
        // 1. 获取输入并初始化
        string inputText = tuumProcessorContent.GetTuumVar<string>(this.Config.InputVariableName) ?? string.Empty;
        this.DebugDto.InputText = inputText;
        var capturedValues = new Dictionary<string, string>();
        var regexOptions = this.BuildRegexOptions();

        try
        {
            // 2. 聚合所有命名捕获组
            foreach (var pattern in this.Config.ExtractionPatterns)
            {
                if (string.IsNullOrWhiteSpace(pattern.Pattern)) continue;

                var regex = new Regex(pattern.Pattern, regexOptions, TimeSpan.FromSeconds(5));
                var match = regex.Match(inputText);

                if (!match.Success)
                    continue;

                foreach (string groupName in regex.GetGroupNames())
                {
                    if (int.TryParse(groupName, out _)) continue; // 跳过数字命名的组
                    capturedValues[groupName] = match.Groups[groupName].Value;
                }
            }

            this.DebugDto.CapturedValues = capturedValues;

            // 3. 智能填充 TOML 模板
            string filledTemplate = PlaceholderRegex.Replace(this.Config.OutputTemplate, match =>
            {
                string groupName = match.Groups["name"].Value;
                string? typeHint = match.Groups["type"].Success ? match.Groups["type"].Value : null;

                capturedValues.TryGetValue(groupName, out string? capturedStringValue);

                // 这个新的辅助函数会生成正确的替换值，
                // 并且不会为字符串类型添加多余的引号。
                return GetTomlReplacementValue(capturedStringValue, typeHint);
            });
            this.DebugDto.FilledTomlTemplate = filledTemplate;

            // 4. 解析填充后的 TOML
            var modelOptions = new TomlModelOptions { ConvertPropertyName = s => s };
            var model = Toml.ToModel(filledTemplate, options: modelOptions);
            this.DebugDto.ParsedTomlModel = model;

            // 5. 将TOML的顶级键作为变量名，并设置变量
            var finalOutputs = new Dictionary<string, object?>();
            foreach (var kvp in model)
            {
                string variableName = kvp.Key;
                object runtimeValue = TomlRuneHelper.ConvertTomlObjectToRuntimeValue(kvp.Value);
                finalOutputs[variableName] = runtimeValue;
                tuumProcessorContent.MergeTuumVar(variableName, runtimeValue);
            }

            this.DebugDto.FinalOutputs = finalOutputs;

            return Result.Ok().AsCompletedTask();
        }
        catch (Exception ex)
        {
            var error = new Error("模板解析执行失败。", ex);
            this.DebugDto.RuntimeError = error.ToDetailString();
            return Result.Fail(error).AsCompletedTask();
        }
    }

    /// <summary>
    /// 根据捕获的字符串值和类型提示，生成适合在 TOML 模板中进行替换的字符串。
    /// - 对于非字符串类型，返回其 TOML 字面量（如 "123", "true", "1.23"）。
    /// - 对于字符串类型，返回经过转义的、可以安全地嵌入到模板已有引号中的 *内容*。
    /// </summary>
    private static string GetTomlReplacementValue(string? value, string? typeHint)
    {
        string valueOrEmpty = value ?? string.Empty;

        switch (typeHint)
        {
            case "int":
                return long.TryParse(valueOrEmpty, NumberStyles.Any, CultureInfo.InvariantCulture, out long i)
                    ? i.ToString(CultureInfo.InvariantCulture)
                    : "0";
            case "float":
                return double.TryParse(valueOrEmpty, NumberStyles.Any, CultureInfo.InvariantCulture, out double f)
                    ? f.ToString("G17", CultureInfo.InvariantCulture)
                    : "0.0";
            case "bool":
                return bool.TryParse(valueOrEmpty, out bool b) && b
                    ? "true"
                    : "false";
            
            // 默认处理为字符串类型
            default:
            {
                // 使用 Tomlyn 序列化一个临时对象来获取正确转义的字符串 *字面量*。
                // 例如，如果 valueOrEmpty 是 "line1\nline2\"quote\""，
                // tomlSnippet 将是 "v = \"line1\\nline2\\\"quote\\\"\""。
                var tempModel = new TomlTable { ["v"] = valueOrEmpty };
                string tomlSnippet = Toml.FromModel(tempModel);

                int valuePartIndex = tomlSnippet.IndexOf('=');
                if (valuePartIndex == -1) return string.Empty; // 安全检查

                string literal = tomlSnippet.Substring(valuePartIndex + 1).Trim();

                // 我们需要的是引号 *内部* 的内容，所以我们剥离 Tomlyn 添加的外部引号。
                // 例如，对于 "content with \"quotes\""，我们想要的是 'content with \"quotes\"'。
                if (literal.Length >= 2 && literal.StartsWith('"') && literal.EndsWith('"'))
                {
                    return literal.Substring(1, literal.Length - 2);
                }

                // 同样处理多行字符串的情况
                if (literal.Length >= 6 && literal.StartsWith("'''", StringComparison.Ordinal) && literal.EndsWith("'''", StringComparison.Ordinal))
                {
                    return literal.Substring(3, literal.Length - 6);
                }
                
                // 为 Tomlyn 可能生成的其他字面量类型（例如，'...' 形式的字面量字符串）提供回退，
                // 尽管对于任意输入来说这种情况不太可能发生。
                if (literal.Length >= 2 && literal.StartsWith('\'') && literal.EndsWith('\''))
                {
                    return literal.Substring(1, literal.Length - 2);
                }
                
                return valueOrEmpty; // 最后的备用逻辑，应该很少被触发。
            }
        }
    }
    /// <summary>
    /// 使用 Tomlyn 序列化器将 C# 对象转换为其 TOML 字面量表示形式的字符串。
    /// 这从根本上解决了所有转义和格式化问题。
    /// </summary>
    private static string SerializeTomlLiteral(object value)
    {
        // 创建一个临时模型，其中包含我们要序列化的值
        var tempModel = new TomlTable { ["v"] = value };

        // 让 Tomlyn 将此模型序列化为字符串，例如 "v = \"some string with \\\"quotes\\\"\""
        string tomlSnippet = Toml.FromModel(tempModel);

        // 提取等号后面的部分，即值的字面量表示
        int valuePartIndex = tomlSnippet.IndexOf('=');
        if (valuePartIndex >= 0)
        {
            return tomlSnippet.Substring(valuePartIndex + 1).Trim();
        }

        // 如果发生意外，返回一个安全的默认值（TOML空字符串）
        return "''";
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
        public TomlTable? ParsedTomlModel { get; set; }
        public Dictionary<string, object?>? FinalOutputs { get; set; }
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
    private static IAppLogger Logger { get; } = AppLogging.CreateLogger<TemplateParserRuneProcessor>();

    private const string GroupRegex = "全局正则选项";
    private static readonly Regex PlaceholderForSpecRegex = GetPlaceholderForSpecRegex();

    [GeneratedRegex(@"\$\{(?<name>\w+)(?::(?<type>\w+))?\}", RegexOptions.Compiled)]
    private static partial Regex GetPlaceholderForSpecRegex();

    #region Config Properties

    [Required] [Display(Name = "输入变量名")] public string InputVariableName { get; init; } = "AiOutput";

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
    public List<ExtractionPattern> ExtractionPatterns { get; init; } = [];

    /// <summary>
    /// 定义一个用于文本提取的正则表达式模式。
    /// </summary>
    public record ExtractionPattern
    {
        /// <summary>
        /// 用于从输入文本中捕获命名组的正则表达式
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        [Display(Name = "正则表达式", Description = "用于从输入文本中捕获命名组的正则表达式。")]
        public string Pattern { get; init; } = string.Empty;
    }

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

        string preprocessedTemplate = PreprocessTomlTemplateForSpec(this.OutputTemplate);
        Logger.Info("正在准备{PreprocessedTemplate}", preprocessedTemplate);

        try
        {
            // 1. 使用 Tomlyn 解析脚本内容
            var model = Toml.ToModel(preprocessedTemplate);
            var specs = new List<ProducedSpec>();

            // 2. 遍历 TOML 模型的顶层键
            foreach (string key in model.Keys)
            {
                object tomlObject = model[key];
                // 3. 递归地将 TOML 对象转换为 VarSpecDef
                var varDef = TomlRuneHelper.ConvertTomlObjectToVarSpecDef(tomlObject);
                specs.Add(new ProducedSpec(key, varDef));
            }

            return specs;
        }
        catch
        {
            // 解析失败，返回空列表或错误标记
            return [];
        }
    }

    private static string PreprocessTomlTemplateForSpec(string template)
    {
        return PlaceholderForSpecRegex.Replace(template, match =>
        {
            string typeHint = match.Groups["type"].Value;
            return typeHint switch
            {
                "int" => "0",
                "float" => "0.0",
                "bool" => "true",
                // 用一个简单的、不带引号的虚拟值替换，它将被放入模板中已存在的引号内
                _ => "dummy_string_for_spec"
            };
        });
    }

    #endregion

    protected override TemplateParserRuneProcessor ToCurrentRune(ICreatingContext creatingContext) => new(this, creatingContext);
}