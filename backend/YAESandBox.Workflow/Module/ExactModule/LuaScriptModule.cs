using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using NLua;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.DebugDto;
using static YAESandBox.Workflow.Module.ExactModule.LuaScriptModuleProcessor;
using static YAESandBox.Workflow.Step.StepProcessor;

// ReSharper disable InconsistentNaming

namespace YAESandBox.Workflow.Module.ExactModule;

/// <summary>
/// Lua 脚本模块处理器。
/// 负责执行用户提供的 Lua 脚本，并通过一个安全桥接器与步骤上下文交互。
/// </summary>
/// <param name="config">模块配置。</param>
internal partial class LuaScriptModuleProcessor(LuaScriptModuleConfig config)
    : IWithDebugDto<LuaScriptModuleProcessorDebugDto>, INormalModule
{
    private LuaScriptModuleConfig Config { get; } = config;

    /// <inheritdoc />
    public LuaScriptModuleProcessorDebugDto DebugDto { get; } = new();

    /// <summary>
    /// 执行 Lua 脚本。
    /// </summary>
    public Task<Result> ExecuteAsync(StepProcessorContent stepProcessorContent, CancellationToken cancellationToken = default)
    {
        this.DebugDto.ExecutedScript = this.Config.Script;

        try
        {
            // 使用 using 确保 Lua 状态机被正确释放
            using var lua = new Lua();

            // --- 沙箱化：移除危险的内建模块 ---
            // 通过将这些库的全局变量设为 nil，来阻止脚本访问文件系统、执行系统命令等不安全操作
            lua.DoString(@"
                os = nil
                io = nil
                debug = nil
                package = nil
                dofile = nil
                loadfile = nil
                require = nil
            ");

            // --- 注入安全 API 模块 ---

            // 1. 上下文模块 (ctx)
            lua["ctx"] = new LuaContextBridge(stepProcessorContent);

            // 2. 日志模块 (log)
            lua["log"] = new LuaLogBridge(this.DebugDto);

            // 3. JSON 模块 (json)
            lua["json"] = new LuaJsonBridge();

            // 4. 正则表达式模块 (regex)
            lua["regex"] = new LuaRegexBridge();

            lua["datetime"] = new LuaDateTimeBridge();

            // 执行脚本
            lua.DoString(this.Config.Script);

            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            // 捕获 Lua 语法错误或运行时错误
            string errorMessage = $"执行 Lua 脚本时发生错误: {ex.Message}";
            this.DebugDto.RuntimeError = errorMessage;
            return Task.FromResult(Result.Fail(errorMessage).ToResult());
        }
    }
}

/// <summary>
/// Lua 脚本模块的配置。
/// </summary>
[ClassLabel("📜Lua")]
internal partial record LuaScriptModuleConfig : AbstractModuleConfig<LuaScriptModuleProcessor>
{
    /// <summary>
    /// 用户编写的 Lua 脚本。
    /// 脚本可以通过全局变量 `ctx` 与工作流交互，
    /// 使用 `ctx.get('var_name')` 获取变量，
    /// 使用 `ctx.set('var_name', value)` 设置变量。
    /// </summary>
    [Required]
    [DataType(DataType.MultilineText)]
    [RenderWithMonacoEditor("lua", SimpleConfigUrl = "/plugins/LuaEditor/monaco-lua-service.js")]
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
    public required string Script { get; init; } = "";

    /// <inheritdoc />
    protected override LuaScriptModuleProcessor ToCurrentModule(WorkflowRuntimeService workflowRuntimeService) => new(this);

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
    internal override List<string> GetConsumedVariables()
    {
        if (string.IsNullOrWhiteSpace(this.Script))
        {
            return [];
        }

        return ConsumedVariableRegex().Matches(this.Script)
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// 通过静态分析 Lua 脚本，提取所有通过 `ctx.set()` 生产的变量。
    /// </summary>
    internal override List<string> GetProducedVariables()
    {
        if (string.IsNullOrWhiteSpace(this.Script))
        {
            return [];
        }

        return ProducedVariableRegex().Matches(this.Script)
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .ToList();
    }
}