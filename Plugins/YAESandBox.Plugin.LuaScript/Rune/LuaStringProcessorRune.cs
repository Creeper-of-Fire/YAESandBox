using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Plugin.LuaScript.LuaRunner;
using YAESandBox.Plugin.LuaScript.LuaRunner.Bridge;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Rune;
using YAESandBox.Workflow.Rune.Config;
using YAESandBox.Workflow.Runtime;
using YAESandBox.Workflow.VarSpec;
using static YAESandBox.Workflow.Tuum.TuumProcessor;

namespace YAESandBox.Plugin.LuaScript.Rune;

/// <summary>
/// Lua 字符串处理符文处理器。
/// 专注于接收一个字符串，通过 Lua 脚本处理，并输出一个字符串。
/// </summary>
public class LuaStringProcessorRuneProcessor(LuaStringProcessorRuneConfig config, ICreatingContext creatingContext)
    : NormalRune<LuaStringProcessorRuneConfig, LuaStringProcessorRuneProcessor.LuaStringProcessorRuneDebugDto>(config, creatingContext)
{
    /// <summary>
    /// 执行 Lua 字符串处理脚本。
    /// </summary>
    /// <inheritdoc />
    public override Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
    {
        string script = this.Config.Script ?? "";
        this.DebugDto.ExecutedScript = script;

        // 1. 从枢机上下文中获取输入值，并健壮地转换为字符串
        object? rawInputValue = tuumProcessorContent.GetTuumVar(this.Config.InputVariableName);
        string inputForLua = rawInputValue?.ToString() ?? string.Empty;

        // 2. 准备一个变量来接收从 Lua 返回的结果
        string finalOutput = string.Empty;

        // 3. 创建一个回调函数，供 Lua 设置输出值
        //    使用 object? 接收，以应对 Lua 可能返回非字符串类型的情况
        Action<object?> setOutputCallback = luaResult => { finalOutput = luaResult?.ToString() ?? string.Empty; };

        // 4. 使用构建器创建一个不含 "ctx" 桥的精简版 Lua 运行器
        var runner = new LuaRunnerBuilder(tuumProcessorContent, this.DebugDto)
            .AddBridge(new LuaJsonBridge())
            .AddBridge(new LuaRegexBridge())
            .AddBridge(new LuaDateTimeBridge())
            .Build();

        // 5. 执行脚本，并通过 preExecutionSetup 注入我们的全局变量和函数
        var result = runner.ExecuteAsync(script,
            preExecutionSetup: luaState =>
            {
                luaState["input_string"] = inputForLua;
                luaState["set_output"] = setOutputCallback;
            },
            cancellationToken: cancellationToken);

        // Task<Result> 是同步完成的，所以可以直接检查结果
        if (result.Result.TryGetError(out var error))
        {
            return result; // 传播错误
        }

        // 6. 将处理后的结果写回枢机上下文
        tuumProcessorContent.SetTuumVar(this.Config.OutputVariableName, finalOutput);

        return Result.Ok().AsCompletedTask();
    }

    /// <inheritdoc />
    public record LuaStringProcessorRuneDebugDto : LuaScriptRuneProcessor.LuaScriptRuneProcessorDebugDto;
}

/// <summary>
/// Lua 字符串处理符文的配置。
/// </summary>
[ClassLabel("📜Lua解析")]
public record LuaStringProcessorRuneConfig : AbstractRuneConfig<LuaStringProcessorRuneProcessor>
{
    /// <summary>
    /// 要处理的输入变量的名称。
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    [Display(Name = "输入变量名", Description = "指定要从枢机上下文中读取哪个变量作为输入。")]
    public string InputVariableName { get; init; } = string.Empty;

    /// <summary>
    /// 处理完成后要写入的输出变量的名称。
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    [Display(Name = "输出变量名", Description = "指定处理结果要写入到哪个枢机变量中。")]
    public string OutputVariableName { get; init; } = string.Empty;

    private const string DefaultScript =
        """
        -- 这是一个透传脚本，它会将输入直接作为输出。
        -- 您可以在此基础上进行修改。
        local input = input_string

        log.info('接收到输入: ' .. input)

        -- 将接收到的输入字符串直接设置为输出
        set_output(input)

        log.info('已将输入直接透传为输出。')
        """;

    /// <summary>
    /// 用户编写的 Lua 脚本。
    /// 脚本可以通过全局只读变量 `input_string` 获取输入，
    /// 并通过全局函数 `set_output(value)` 设置输出。
    /// </summary>
    [DataType(DataType.MultilineText)]
    [RenderWithMonacoEditor("lua", SimpleConfigUrl = "plugin://lua-string/monaco-lua-service-string.js")]
    [Display(
        Name = "Lua 处理脚本",
        Description = "在此编写 Lua 脚本。使用 `input_string` 获取输入，使用 `set_output(result)` 设置输出。"
    )]
    [DefaultValue(DefaultScript)]
    [Required(AllowEmptyStrings = true)]
    public string Script { get; init; } = DefaultScript;

    /// <inheritdoc />
    protected override LuaStringProcessorRuneProcessor ToCurrentRune(ICreatingContext creatingContext) => new(this, creatingContext);

    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec() => [new(this.InputVariableName, CoreVarDefs.String) { IsOptional = false }];

    /// <inheritdoc />
    public override List<ProducedSpec> GetProducedSpec() => [new(this.OutputVariableName, CoreVarDefs.String)];
}