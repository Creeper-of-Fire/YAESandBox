using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Plugin.LuaScript.LuaRunner;
using YAESandBox.Plugin.LuaScript.LuaRunner.Bridge;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Rune;
using YAESandBox.Workflow.Rune.Config;
using YAESandBox.Workflow.Runtime;
using YAESandBox.Workflow.VarSpec;
using static YAESandBox.Workflow.Tuum.TuumProcessor;

// ReSharper disable InconsistentNaming

namespace YAESandBox.Plugin.LuaScript.Rune;

/// <summary>
/// Lua 脚本符文处理器。
/// 负责执行用户提供的 Lua 脚本，并通过一个安全桥接器与枢机上下文交互。
/// </summary>
/// <param name="config">符文配置。</param>
public class LuaScriptRuneProcessor(LuaScriptRuneConfig config,ICreatingContext creatingContext)
    : NormalRune<LuaScriptRuneConfig, LuaScriptRuneProcessor.LuaScriptRuneProcessorDebugDto>(config, creatingContext)
{

    /// <summary>
    /// 执行 Lua 脚本。
    /// </summary>
    /// <inheritdoc />
    public override Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
    {
        string script = this.Config.Script ?? "";
        this.DebugDto.ExecutedScript = script;

        // 创建并使用通用的 Lua 脚本执行器
        var runner = new LuaRunnerBuilder(tuumProcessorContent, this.DebugDto)
            .AddBridge(new LuaJsonBridge())
            .AddBridge(new LuaContextBridge(tuumProcessorContent)) // 添加 ctx 功能
            .AddBridge(new LuaRegexBridge()) // 添加 regex 功能
            .AddBridge(new LuaDateTimeBridge()) // 添加 datetime 功能
            .Build();

        // 直接执行脚本，无需其他设置
        return runner.ExecuteAsync(script, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Lua 脚本符文处理器的调试数据传输对象。
    /// </summary>
    public record LuaScriptRuneProcessorDebugDto : IRuneProcessorDebugDto, IDebugDtoWithLogs
    {
        /// <summary>
        /// 实际执行的 Lua 脚本内容。
        /// </summary>
        public string? ExecutedScript { get; set; }

        /// <summary>
        /// 脚本执行期间发生的运行时错误（如果有）。
        /// </summary>
        public string? RuntimeError { get; set; }

        /// <summary>
        /// 脚本通过 log 符文输出的日志。
        /// </summary>
        public List<string> Logs { get; } = new();
    }
}

/// <summary>
/// Lua 脚本符文的配置。
/// </summary>
[ClassLabel("📜Lua")]
public partial record LuaScriptRuneConfig : AbstractRuneConfig<LuaScriptRuneProcessor>
{
    /// <summary>
    /// 用户编写的 Lua 脚本。
    /// 脚本可以通过全局变量 `ctx` 与工作流交互，
    /// 使用 `ctx.get('var_name')` 获取变量，
    /// 使用 `ctx.set('var_name', value)` 设置变量。
    /// 可以通过在前一行添加 `-- @type TypeName Description...` 的注释来为变量指定类型和描述。
    /// </summary>
    [DataType(DataType.MultilineText)]
    [RenderWithMonacoEditor("lua", SimpleConfigUrl = "plugin://lua-main/monaco-lua-service-main.js")]
    [Display(
        Name = "Lua 脚本",
        Description = "在此处编写 Lua 脚本。使用 ctx.get('变量名') 获取输入，使用 ctx.set('变量名', 值) 设置输出。可以在 get/set 的上一行使用 -- @type 类型名 [可选的描述信息] 来指定变量类型。",
        Prompt =
            "-- 示例: 使用类型注解和描述\n\n" +
            "-- @type string 用户的唯一标识符\n" +
            "local user_id = ctx.get('input_user_id')\n\n" +
            "log.info('正在处理用户: ' .. user_id)\n\n" +
            "-- @type: number 计算得出的最终分数\n" +
            "local score = 100\n" +
            "ctx.set('final_score', score)\n\n" +
            "-- @type boolean 指示操作是否成功\n" +
            "ctx.set('is_success', true)\n\n" +
            "-- 没有类型注解的变量将被视为 any 类型\n" +
            "ctx.set('untyped_output', { key = 'value' })\n"
    )]
    [Required(AllowEmptyStrings = true)]
    [DefaultValue("")]
    public string Script { get; init; } = "";

    /// <inheritdoc />
    protected override LuaScriptRuneProcessor ToCurrentRune(ICreatingContext creatingContext) => new(this,creatingContext);

    // --- 变量静态分析 ---

    // 正则表达式用于匹配 ctx.get('...') 或 ctx.get("...")
    // Group 1: (可选) 类型名称 (e.g., 'string')
    // Group 2: (可选) 描述信息
    // Group 3: 变量名称
    [GeneratedRegex(@"(?:--\s*@type[:]?(?:\s*(\S+))(?:\s+(.*?))?\s*\r?\n)?\s*^\s*.*?ctx\.get\s*\(\s*['""]([^'""]+)['""]\s*\)", RegexOptions.Multiline)]
    private static partial Regex ConsumedVariableRegex();

    // 正则表达式用于匹配 ctx.set('...') 或 ctx.set("...")
    // Group 1: (可选) 类型名称 (e.g., 'string')
    // Group 2: (可选) 描述信息
    // Group 3: 变量名称
    [GeneratedRegex(@"(?:--\s*@type[:]?(?:\s*(\S+))(?:\s+(.*?))?\s*\r?\n)?\s*ctx\.set\s*\(\s*['""]([^'""]+)['""]\s*,", RegexOptions.Multiline)]
    private static partial Regex ProducedVariableRegex();


    /// <summary>
    /// 通过静态分析 Lua 脚本，提取所有通过 `ctx.get()` 消费的变量。
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
    /// 通过静态分析 Lua 脚本，提取所有通过 `ctx.set()` 生产的变量。
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