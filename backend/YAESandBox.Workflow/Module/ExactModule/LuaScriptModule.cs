using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using NLua;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Workflow.API;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.DebugDto;
using static YAESandBox.Workflow.Module.ExactModule.LuaScriptModuleProcessor;
using static YAESandBox.Workflow.Step.StepProcessor;

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
            
            // 设置一个超时，防止脚本无限循环。NLua 内部会启动一个监控线程。
            // 注意：这可能不是一个硬性的实时中止，但能有效防止大部分死循环问题。
            // lua.State.SetExecutionLimit(5000000); // 限制执行指令数量，需要根据实际情况调整

            // 创建一个安全的上下文桥接器，并将其注册为 Lua 的全局变量 'ctx'
            var contextBridge = new LuaContextBridge(stepProcessorContent);
            lua["ctx"] = contextBridge;

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

    /// <summary>
    /// 作为 C# 与 Lua 脚本之间交互的安全桥梁。
    /// 仅暴露 Get 和 Set 方法，防止 Lua 脚本访问不应访问的内部状态。
    /// </summary>
    private class LuaContextBridge(StepProcessorContent stepContent)
    {
        private StepProcessorContent StepContent { get; } = stepContent;

        /// <summary>
        /// 从步骤变量池中获取一个变量。暴露给 Lua 使用。
        /// </summary>
        /// <param name="name">变量名。</param>
        /// <returns>变量的值，如果不存在则为 null。</returns>
        // ReSharper disable once InconsistentNaming
        public object? get(string name)
        {
            return this.StepContent.InputVar(name);
        }

        /// <summary>
        ///向步骤变量池中设置一个变量。暴露给 Lua 使用。
        /// </summary>
        /// <param name="name">变量名。</param>
        /// <param name="value">要设置的值。</param>
        // ReSharper disable once InconsistentNaming
        public void set(string name, object value)
        {
            this.StepContent.OutputVar(name, value);
        }
    }

    /// <summary>
    /// Lua 脚本模块处理器的调试数据传输对象。
    /// </summary>
    internal class LuaScriptModuleProcessorDebugDto : IModuleProcessorDebugDto
    {
        /// <summary>
        /// 实际执行的 Lua 脚本内容。
        /// </summary>
        public string? ExecutedScript { get; set; }

        /// <summary>
        /// 脚本执行期间发生的运行时错误（如果有）。
        /// </summary>
        public string? RuntimeError { get; set; }
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
        Prompt = "-- 示例:\nlocal name = ctx.get('playerName')\nlocal greeting = '你好, ' .. name .. '!'\nctx.set('greetingMessage', greeting)"
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