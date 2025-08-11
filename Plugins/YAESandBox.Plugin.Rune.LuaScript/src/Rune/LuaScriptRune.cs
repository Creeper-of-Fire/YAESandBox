using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Plugin.LuaScript.LuaRunner;
using YAESandBox.Plugin.LuaScript.LuaRunner.Bridge;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Rune;
using YAESandBox.Workflow.VarSpec;
using static YAESandBox.Plugin.LuaScript.Rune.LuaScriptRuneProcessor;
using static YAESandBox.Workflow.Tuum.TuumProcessor;

// ReSharper disable InconsistentNaming

namespace YAESandBox.Plugin.LuaScript.Rune;

/// <summary>
/// Lua 脚本符文处理器。
/// 负责执行用户提供的 Lua 脚本，并通过一个安全桥接器与枢机上下文交互。
/// </summary>
/// <param name="config">符文配置。</param>
public partial class LuaScriptRuneProcessor(LuaScriptRuneConfig config)
    : INormalRune<LuaScriptRuneConfig, LuaScriptRuneProcessorDebugDto>
{
    /// <inheritdoc />
    public LuaScriptRuneConfig Config { get; } = config;

    /// <inheritdoc />
    public LuaScriptRuneProcessorDebugDto DebugDto { get; } = new();

    /// <summary>
    /// 执行 Lua 脚本。
    /// </summary>
    public Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
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
    public class LuaScriptRuneProcessorDebugDto : IRuneProcessorDebugDto, IDebugDtoWithLogs
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
    /// </summary>
    [DataType(DataType.MultilineText)]
    [RenderWithMonacoEditor("lua", SimpleConfigUrl = "plugin://lua-main/monaco-lua-service-main.js")]
    [Display(
        Name = "Lua 脚本",
        Description = "在此处编写 Lua 脚本。使用 ctx.get('变量名') 获取输入，使用 ctx.set('变量名', 值) 设置输出。",
        Prompt =
            "-- 示例:\nlog.info('脚本开始执行')\n\n" +
            "-- 获取当前 UTC 时间并格式化\nlocal now_utc = datetime.utcnow()\nctx.set('currentTime', now_utc:format('yyyy-MM-dd HH:mm:ss'))\n\n" +
            "-- 时间计算\nlocal tomorrow = now_utc:add_days(1)\nlog.info('明天的日期是: ' .. tomorrow:format('yyyy-MM-dd'))\n\n" +
            "-- 解析字符串\nlocal my_birthday_str = '1990-05-20'\n" +
            "local birthday_obj = datetime.parse(my_birthday_str, 'yyyy-MM-dd')\nif birthday_obj then\n" +
            "  log.info('生日的年份是: ' .. birthday_obj.year)\n" +
            "end\n"
    )]
    [Required(AllowEmptyStrings = true)]
    [DefaultValue("")]
    public string Script { get; init; } = "";

    /// <inheritdoc />
    protected override LuaScriptRuneProcessor ToCurrentRune(WorkflowRuntimeService workflowRuntimeService) => new(this);

    // --- 变量静态分析 ---

    // 正则表达式用于匹配 ctx.get('...') 或 ctx.get("...")
    [GeneratedRegex(@"ctx\.get\s*\(\s*['""]([^'""]+)['""]\s*\)", RegexOptions.Multiline)]
    private static partial Regex ConsumedVariableRegex();

    // 正则表达式用于匹配 ctx.set('...') 或 ctx.set("...")
    [GeneratedRegex(@"ctx\.set\s*\(\s*['""]([^'""]+)['""]\s*,", RegexOptions.Multiline)]
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
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .Select(name => new ConsumedSpec(name, CoreVarDefs.Any) { IsOptional = false })
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
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .Select(name => new ProducedSpec(name, CoreVarDefs.Any))
            .ToList();
    }
}