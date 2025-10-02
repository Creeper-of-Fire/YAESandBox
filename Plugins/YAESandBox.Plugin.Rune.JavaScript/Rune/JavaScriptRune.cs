using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Jint;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Config.RuneConfig;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Runtime.Processor;
using YAESandBox.Workflow.Runtime.Processor.RuneProcessor;
using YAESandBox.Workflow.VarSpec;
using static YAESandBox.Workflow.Runtime.Processor.TuumProcessor;

// ReSharper disable InconsistentNaming

namespace YAESandBox.Plugin.Rune.JavaScript.Rune;

/// <summary>
/// JavaScript 脚本符文处理器。
/// 负责执行用户提供的 JS 脚本，并通过直接暴露的上下文与枢机交互。
/// </summary>
/// <param name="config">符文配置。</param>
public class JavaScriptRuneProcessor(JavaScriptRuneConfig config,ICreatingContext creatingContext)
    : NormalRuneProcessor<JavaScriptRuneConfig, JavaScriptRuneProcessor.JavaScriptRuneProcessorDebugDto>(config, creatingContext)
{
    /// <summary>
    /// 执行 JavaScript 脚本。
    /// </summary>
    /// <inheritdoc />
    public override Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
    {
        string script = this.Config.Script;
        this.DebugDto.ExecutedScript = script;

        try
        {
            var engine = new Engine(options =>
            {
                // 允许访问 .NET 类
                options.AllowClr();
                // 设置执行超时和递归限制以防止死循环或恶意脚本
                options.TimeoutInterval(TimeSpan.FromSeconds(5));
                options.Constraints.MaxRecursionDepth = 128;
                options.CancellationToken(cancellationToken);
            });

            // 直接暴露枢机上下文
            engine.SetValue("ctx", tuumProcessorContent);

            // 提供一个日志记录器
            var logAction = (string message) => this.DebugDto.Logs.Add(message);
            engine.SetValue("log", new { info = logAction, warn = logAction, error = logAction });

            // 内置 JSON 支持，无需额外桥接

            engine.Execute(script);

            return Result.Ok().AsCompletedTask();
        }
        catch (Exception ex)
        {
            this.DebugDto.RuntimeError = ex.ToString();
            return Result.Fail("JavaScript运行错误。", ex).AsCompletedTask();
        }
    }

    /// <summary>
    /// JavaScript 脚本符文处理器的调试数据传输对象。
    /// </summary>
    public record JavaScriptRuneProcessorDebugDto : IRuneProcessorDebugDto, IDebugDtoWithLogs
    {
        /// <summary>
        /// 实际执行的 JavaScript 脚本内容。
        /// </summary>
        public string? ExecutedScript { get; set; }

        /// <summary>
        /// 脚本执行期间发生的运行时错误（如果有）。
        /// </summary>
        public string? RuntimeError { get; set; }

        /// <summary>
        /// 脚本通过 log 对象输出的日志。
        /// </summary>
        public List<string> Logs { get; } = new();
    }
}

/// <summary>
/// JavaScript 脚本符文的配置。
/// </summary>
[ClassLabel("📜JS")]
public partial record JavaScriptRuneConfig : AbstractRuneConfig<JavaScriptRuneProcessor>
{
    /// <summary>
    /// 用户编写的 JavaScript 脚本。
    /// 脚本可以通过全局变量 `ctx` 与工作流交互，
    /// 使用 `ctx.GetTuumVar('var_name')` 获取变量，
    /// 使用 `ctx.SetTuumVar('var_name', value)` 设置变量。
    /// 可以通过在前一行添加 `// @type TypeName Description...` 的注释来为变量指定类型和描述。
    /// </summary>
    [DataType(DataType.MultilineText)]
    [RenderWithMonacoEditor("javascript", SimpleConfigUrl = "plugin://js-main/monaco-js-service-main.js")]
    [Display(
        Name = "JavaScript 脚本",
        Description =
            "在此处编写 JS 脚本。使用 ctx.GetTuumVar('变量名') 获取输入，使用 ctx.SetTuumVar('变量名', 值) 设置输出。可以在 GetTuumVar/SetTuumVar 的上一行使用 // @type 类型名 [可选的描述信息] 来指定变量类型。",
        Prompt =
            "// 示例: 使用类型注解和描述\n\n" +
            "// @type string 用户的唯一标识符\n" +
            "const user_id = ctx.GetTuumVar('input_user_id');\n\n" +
            "log.info(`正在处理用户: ${user_id}`);\n\n" +
            "// @type number 计算得出的最终分数\n" +
            "let score = 100;\n" +
            "ctx.SetTuumVar('final_score', score);\n\n" +
            "// @type boolean 指示操作是否成功\n" +
            "ctx.SetTuumVar('is_success', true);\n\n" +
            "// 没有类型注解的变量将被视为 any 类型\n" +
            "ctx.SetTuumVar('untyped_output', { key: 'value' });\n"
    )]
    [Required(AllowEmptyStrings = true)]
    [DefaultValue("")]
    public string Script { get; init; } = "";

    /// <inheritdoc />
    protected override JavaScriptRuneProcessor ToCurrentRune(ICreatingContext creatingContext) => new(this,creatingContext);

    // --- 变量静态分析 ---

    // 正则表达式用于匹配 ctx.GetTuumVar('...') 或 ctx.GetTuumVar("...")
    // Group 1: (可选) 类型名称 (e.g., 'string')
    // Group 2: (可选) 描述信息
    // Group 3: 变量名称
    [GeneratedRegex(@"(?://\s*@type[:]?(?:\s*(\S+))(?:\s+(.*?))?\s*\r?\n)?\s*.*?ctx\.GetTuumVar\s*\(\s*['""]([^'""]+)['""]\s*\)",
        RegexOptions.Multiline)]
    private static partial Regex ConsumedVariableRegex();

    // 正则表达式用于匹配 ctx.SetTuumVar('...') 或 ctx.SetTuumVar("...")
    // Group 1: (可选) 类型名称 (e.g., 'string')
    // Group 2: (可选) 描述信息
    // Group 3: 变量名称
    [GeneratedRegex(@"(?://\s*@type[:]?(?:\s*(\S+))(?:\s+(.*?))?\s*\r?\n)?\s*ctx\.SetTuumVar\s*\(\s*['""]([^'""]+)['""]\s*,",
        RegexOptions.Multiline)]
    private static partial Regex ProducedVariableRegex();


    /// <summary>
    /// 通过静态分析 JS 脚本，提取所有通过 `ctx.GetTuumVar()` 消费的变量。
    /// </summary>
    public override List<ConsumedSpec> GetConsumedSpec()
    {
        if (string.IsNullOrWhiteSpace(this.Script))
        {
            return [];
        }

        return ConsumedVariableRegex().Matches(this.Script)
            .Select(match => new
            {
                TypeName = match.Groups[1].Value,
                Description = match.Groups[2].Value.Trim(),
                VarName = match.Groups[3].Value
            })
            .GroupBy(v => v.VarName) // 按变量名分组，以处理同一变量的多次 get
            .Select(group =>
            {
                string varName = group.Key;
                // 优先使用第一个找到的带有类型注解的条目
                var bestAnnotation = group.FirstOrDefault(g => !string.IsNullOrWhiteSpace(g.TypeName));

                string? typeName = bestAnnotation?.TypeName;
                string? description = bestAnnotation?.Description;

                VarSpecDef varDef;
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    varDef = CoreVarDefs.Any; // 如果没有注解，则默认为 Any 类型
                }
                else
                {
                    string? finalDescription = string.IsNullOrWhiteSpace(description) ? null : description;
                    varDef = new VarSpecDef(typeName, finalDescription);
                }

                return new ConsumedSpec(varName, varDef) { IsOptional = false };
            })
            .ToList();
    }

    /// <summary>
    /// 通过静态分析 JS 脚本，提取所有通过 `ctx.SetTuumVar()` 生产的变量。
    /// </summary>
    public override List<ProducedSpec> GetProducedSpec()
    {
        if (string.IsNullOrWhiteSpace(this.Script))
        {
            return [];
        }

        return ProducedVariableRegex().Matches(this.Script)
            .Select(match => new
            {
                TypeName = match.Groups[1].Value,
                Description = match.Groups[2].Value.Trim(),
                VarName = match.Groups[3].Value
            })
            .GroupBy(v => v.VarName) // 按变量名分组，以处理同一变量的多次 set
            .Select(group =>
            {
                string varName = group.Key;
                // 优先使用第一个找到的带有类型注解的条目
                var bestAnnotation = group.FirstOrDefault(g => !string.IsNullOrWhiteSpace(g.TypeName));

                string? typeName = bestAnnotation?.TypeName;
                string? description = bestAnnotation?.Description;

                VarSpecDef varDef;
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    varDef = CoreVarDefs.Any; // 如果没有注解，则默认为 Any 类型
                }
                else
                {
                    string? finalDescription = string.IsNullOrWhiteSpace(description) ? null : description;
                    varDef = new VarSpecDef(typeName, finalDescription);
                }

                return new ProducedSpec(varName, varDef);
            })
            .ToList();
    }
}