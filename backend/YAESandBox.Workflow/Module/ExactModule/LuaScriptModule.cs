using System.ComponentModel;
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
        string script = this.Config.Script ?? "";
        this.DebugDto.ExecutedScript = script;

        try
        {
            // 使用 using 确保 Lua 状态机被正确释放
            using var lua = new Lua();
            
            lua.State.Encoding = System.Text.Encoding.UTF8;

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


            
            // 1. 创建所有桥接类的实例
            var logger = new LuaLogBridge(this.DebugDto);
            var contextBridge = new LuaContextBridge(stepProcessorContent, logger);
            var jsonBridge = new LuaJsonBridge(logger);
            var regexBridge = new LuaRegexBridge(logger);
            var dateTimeBridge = new LuaDateTimeBridge(logger);

            // 2. 手动创建 Lua Table 并注册函数委托
            
            // 注册 log.*
            lua.NewTable("log");
            var logTable = lua["log"] as LuaTable;
            logTable["info"] = logger.info;
            logTable["warn"] = logger.warn;
            logTable["error"] = logger.error;

            // 注册 ctx.*
            lua.NewTable("ctx");
            var ctxTable = lua["ctx"] as LuaTable;
            ctxTable["get"] = contextBridge.get;
            ctxTable["set"] = contextBridge.set;

            // 注册 json.*
            lua.NewTable("json");
            var jsonTable = lua["json"] as LuaTable;
            jsonTable["encode"] = jsonBridge.encode;
            jsonTable["decode"] = jsonBridge.decode;

            // 注册 regex.*
            lua.NewTable("regex");
            var regexTable = lua["regex"] as LuaTable;
            regexTable["is_match"] = regexBridge.is_match;
            regexTable["match"] = regexBridge.match;
            regexTable["match_all"] = regexBridge.match_all;

            // 注册 datetime.*
            lua.NewTable("datetime");
            var datetimeTable = lua["datetime"] as LuaTable;
            datetimeTable["utcnow"] = dateTimeBridge.utcnow;
            datetimeTable["now"] = dateTimeBridge.now;
            datetimeTable["parse"] = dateTimeBridge.parse;

            // --- 执行脚本 ---
            lua.DoString(script);

            return Task.FromResult(Result.Ok());
        }
        catch (Exception ex)
        {
            // --- 全新的、更详细的错误处理 ---

            // 1. 构建一个简洁但信息更丰富的错误消息，包含异常类型和主消息。
            //    我们还会检查内部异常，因为真正的错误根源常常在那里。
            var primaryException = ex.InnerException ?? ex;
            string conciseErrorMessage = $"执行 Lua 脚本时发生错误: [{primaryException.GetType().Name}] {primaryException.Message}";

            // 2. 获取完整的异常详情，包括所有内部异常和完整的堆栈跟踪。
            //    ex.ToString() 是获取此信息的最佳方式。
            string fullErrorDetails = ex.ToString();

            // 3. 将信息记录到调试对象中。
            this.DebugDto.RuntimeError = conciseErrorMessage; // 在 UI 上显示一个清晰的错误摘要。

            // 将完整的、多行的堆栈跟踪信息添加到日志中，供开发者深入分析。
            this.DebugDto.Logs.Add($"[FATAL] 脚本执行异常. 详细信息如下:");
            // 使用 StringReader 逐行添加，以保持格式清晰
            using (var reader = new StringReader(fullErrorDetails))
            {
                for (string? line = reader.ReadLine(); line != null; line = reader.ReadLine())
                {
                    this.DebugDto.Logs.Add(line);
                }
            }

            // 4. 返回失败结果，将简洁的错误消息传递给工作流引擎。
            return Task.FromResult(Result.Fail(fullErrorDetails).ToResult());
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
    [DefaultValue("")]
    public string? Script { get; init; } = "";

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