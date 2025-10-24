using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jint;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Depend.Storage;
using YAESandBox.Workflow.Core.Config.RuneConfig;
using YAESandBox.Workflow.Core.DebugDto;
using YAESandBox.Workflow.Core.Runtime.Processor;
using YAESandBox.Workflow.Core.Runtime.Processor.RuneProcessor;
using YAESandBox.Workflow.Core.VarSpec;
using YAESandBox.Workflow.Schema;
using static YAESandBox.Workflow.Core.Runtime.Processor.TuumProcessor;

// ReSharper disable InconsistentNaming

namespace YAESandBox.Plugin.Rune.JavaScript.Rune;

/// <summary>
/// JavaScript 脚本符文处理器。
/// 负责执行用户提供的 JS 脚本，并通过直接暴露的上下文与枢机交互。
/// </summary>
/// <param name="config">符文配置。</param>
/// <param name="creatingContext"></param>
public class JavaScriptRuneProcessor(JavaScriptRuneConfig config, ICreatingContext creatingContext)
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
[ClassLabel("JS", Icon = "📜")]
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
            """
            // 示例: 使用类型注解和描述

            // 简单类型
            // @type string 用户的唯一标识符
            const user_id = ctx.GetTuumVar('input_user_id');

            // 列表类型
            // @type String[] 用户的标签列表
            const tags = ctx.GetTuumVar('tags');

            // 结构化类型 (Record)
            // @type Record 玩家的详细信息
            /*
            {
              "name": "String",
              "level": "Int"
            }
            */
            const player = ctx.GetTuumVar('player_info');
            log.info(`正在处理玩家: ${player.name}`);

            // @type Record
            /*
            { "success": "Boolean", "message": "String" }
            */
            ctx.SetTuumVar('operation_result', { success: true, message: 'OK' });
            """
    )]
    [Required(AllowEmptyStrings = true)]
    [DefaultValue("")]
    public string Script { get; init; } = "";

    /// <inheritdoc />
    protected override JavaScriptRuneProcessor ToCurrentRune(ICreatingContext creatingContext) => new(this, creatingContext);

    // --- 变量静态分析 ---

    [GeneratedRegex(
        """
        (?://\s*@type[:]?(?:\s*(\S+))|/\*\s*([\s\S]+?)\s*\*/)? # Group 1: TypeName, Group 2: JSON Block
        (?:\s+(.*?))?                                          # Group 3: Description
        \s*\r?\n.*?
        ctx\.(GetTuumVar|SetTuumVar)\s*\(\s*['"]([^'""]+)['"]   # Group 4: Method, Group 5: VarName
        """,
        RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace
    )]
    private static partial Regex VariableRegex();

    /// <summary>
    /// 转换逻辑：将类型名和可选的JSON块转换为具体的 VarSpecDef。
    /// </summary>
    private static VarSpecDef ConvertToVarSpecDef(string typeName, string? jsonBlock, string? description)
    {
        // 1. 处理结构化 Record 类型
        if (!string.IsNullOrWhiteSpace(jsonBlock))
        {
            try
            {
                var properties = YaeSandBoxJsonHelper.Deserialize<Dictionary<string, string>>(jsonBlock);

                if (properties != null)
                {
                    var specProperties = properties.ToDictionary(
                        kvp => kvp.Key,
                        kvp => ConvertToVarSpecDef(kvp.Value, null, null) // 递归转换属性
                    );
                    return new RecordVarSpecDef(CoreVarDefs.RecordStringAny.TypeName, description, specProperties);
                }
            }
            catch (JsonException)
            {
                /* 解析失败，回退到 Any */
            }
        }

        // 2. 处理列表类型 (e.g., "String[]", "ThingInfo[]")
        if (typeName.EndsWith("[]", StringComparison.Ordinal))
        {
            string elementTypeName = typeName[..^2];
            var elementDef = ConvertToVarSpecDef(elementTypeName, null, null);
            return new ListVarSpecDef($"{elementDef.TypeName}[]", description, elementDef);
        }

        // 3. 处理基础类型和已定义的扩展类型
        return typeName.ToLowerInvariant() switch
        {
            "string" => CoreVarDefs.String with { Description = description },
            "int" => CoreVarDefs.Int with { Description = description },
            "float" => CoreVarDefs.Float with { Description = description },
            "boolean" => CoreVarDefs.Boolean with { Description = description },
            "jsonstring" => CoreVarDefs.JsonString with { Description = description },
            "any" => CoreVarDefs.Any with { Description = description },
            "promptlist" => CoreVarDefs.PromptList with { Description = description },
            "thinginfo" => ExtendVarDefs.ThingInfo with { Description = description },
            _ => new PrimitiveVarSpecDef(typeName, description)
        };
    }

    private List<(string VarName, VarSpecDef Def)> ParseSpecs(string targetMethod)
    {
        if (string.IsNullOrWhiteSpace(this.Script)) return [];

        // 注意：正则表达式的捕获组索引可能需要调整
        // JS 正则: Group 1 (TypeName), Group 2 (JsonBlock), Group 3 (Description), Group 4 (Method), Group 5 (VarName)
        // 我们将 TypeName 和 JsonBlock 合并处理

        return VariableRegex().Matches(this.Script)
            .Where(match => match.Groups[4].Value == targetMethod)
            .Select(match => new
            {
                TypeName = match.Groups[1].Value,
                JsonBlock = match.Groups[2].Value,
                Description = match.Groups[3].Value.Trim(),
                VarName = match.Groups[5].Value
            })
            .GroupBy(v => v.VarName)
            .Select(group =>
            {
                string varName = group.Key;
                // 优先使用带有类型注解或JSON块的条目
                var bestAnnotation =
                    group.FirstOrDefault(g => !string.IsNullOrWhiteSpace(g.TypeName) || !string.IsNullOrWhiteSpace(g.JsonBlock));

                string typeName = bestAnnotation?.TypeName ?? "Any";
                string? jsonBlock = bestAnnotation?.JsonBlock;
                string? description = string.IsNullOrWhiteSpace(bestAnnotation?.Description) ? null : bestAnnotation.Description;

                // 如果 JSON 块存在，但 TypeName 为空，则默认 TypeName 为 "Record"
                if (!string.IsNullOrWhiteSpace(jsonBlock) && string.IsNullOrWhiteSpace(typeName))
                {
                    typeName = "Record";
                }

                var varDef = ConvertToVarSpecDef(typeName, jsonBlock, description);
                return (varName, varDef);
            })
            .ToList();
    }

    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec() =>
        this.ParseSpecs("GetTuumVar").Select(v => new ConsumedSpec(v.VarName, v.Def) { IsOptional = false }).ToList();

    /// <inheritdoc />
    public override List<ProducedSpec> GetProducedSpec() =>
        this.ParseSpecs("SetTuumVar").Select(v => new ProducedSpec(v.VarName, v.Def)).ToList();
}