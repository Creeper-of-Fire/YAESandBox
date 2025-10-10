using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Depend.Storage;
using YAESandBox.Plugin.LuaScript.LuaRunner;
using YAESandBox.Plugin.LuaScript.LuaRunner.Bridge;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Config.RuneConfig;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Runtime.Processor;
using YAESandBox.Workflow.Runtime.Processor.RuneProcessor;
using YAESandBox.Workflow.VarSpec;
using static YAESandBox.Workflow.Runtime.Processor.TuumProcessor;

// ReSharper disable InconsistentNaming

namespace YAESandBox.Plugin.LuaScript.Rune;

/// <summary>
/// Lua 脚本符文处理器。
/// 负责执行用户提供的 Lua 脚本，并通过一个安全桥接器与枢机上下文交互。
/// </summary>
/// <param name="config">符文配置。</param>
/// <param name="creatingContext"></param>
public class LuaScriptRuneProcessor(LuaScriptRuneConfig config, ICreatingContext creatingContext)
    : NormalRuneProcessor<LuaScriptRuneConfig, LuaScriptRuneProcessor.LuaScriptRuneProcessorDebugDto>(config, creatingContext)
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
            """
            -- 示例: 使用类型注解和描述

            -- 简单类型
            -- @type string 用户的唯一标识符
            local user_id = ctx.get('input_user_id')

            -- 列表类型
            -- @type String[] 用户的标签列表
            local tags = ctx.get('tags')

            -- 结构化类型 (Record)

            -- @type Record 玩家的详细信息
            --[[
            {
              "name": "String",
              "level": "Number",
              "is_active": "Boolean"
            }
            ]]
            local player = ctx.get('player_info')
            log.info('正在处理玩家: ' .. player.name)

            -- @type Record
            --[[
            { "success": "Boolean", "message": "String" }
            ]]
            ctx.set('operation_result', { success = true, message = 'OK' })

            -- @type: number 计算得出的最终分数
            local score = 100
            ctx.set('final_score', score)

            -- @type boolean 指示操作是否成功
            ctx.set('is_success', true)

            -- 没有类型注解的变量将被视为 any 类型
            ctx.set('untyped_output', { key = 'value' })

            """
    )]
    [Required(AllowEmptyStrings = true)]
    [DefaultValue("")]
    public string Script { get; init; } = "";

    /// <inheritdoc />
    protected override LuaScriptRuneProcessor ToCurrentRune(ICreatingContext creatingContext) => new(this, creatingContext);

    // --- 变量静态分析 ---

    // 正则表达式
    // Group 1: (可选) 类型名称 (e.g., 'string', 'String[]', 'Record')
    // Group 2: (可选) JSON 结构定义块
    // Group 3: (可选) 描述信息
    // Group 4: 变量名称
    [GeneratedRegex(
        """
        (?:--\s*@type[:]?(?:\s*(\S+))(?:\s*\r?\n--\[\[\r?\n([\s\S]+?)\r?\n--\]\])?(?:\s+(.*?))?\s*\r?\n)?\s*
        (?:local\s+\S+\s*=\s*)?                              # 匹配可选的 "local var = " 部分
        ctx\.(?:get|set)\s*\(\s*['"]([^'""]+)['"] # 匹配 ctx.get/set('var_name')
        """,
        RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace
    )]
    private static partial Regex VariableRegex();

    private IEnumerable<(string VarName, VarSpecDef Def)> ParseSpecs(bool isConsumed)
    {
        if (string.IsNullOrWhiteSpace(this.Script)) return [];

        string targetMethod = isConsumed ? "get" : "set";

        return VariableRegex().Matches(this.Script)
            .Where(match =>
            {
                // 手动检查是 get 还是 set，因为正则无法优雅地捕获
                int callIndex = match.Value.IndexOf("ctx." + targetMethod, StringComparison.Ordinal);
                return callIndex != -1;
            })
            .Select(match => new
            {
                TypeName = match.Groups[1].Value,
                JsonBlock = match.Groups[2].Value,
                Description = match.Groups[3].Value.Trim(),
                VarName = match.Groups[4].Value
            })
            .GroupBy(v => v.VarName)
            .Select(group =>
            {
                string varName = group.Key;
                var bestAnnotation = group.FirstOrDefault(g => !string.IsNullOrWhiteSpace(g.TypeName));

                string typeName = bestAnnotation?.TypeName ?? "Any";
                string? jsonBlock = bestAnnotation?.JsonBlock;
                string? description = string.IsNullOrWhiteSpace(bestAnnotation?.Description) ? null : bestAnnotation.Description;

                var varDef = ConvertToVarSpecDef(typeName, jsonBlock, description);

                return (varName, varDef);
            })
            .ToList();
    }

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
            "number" => CoreVarDefs.Number with { Description = description },
            "boolean" => CoreVarDefs.Boolean with { Description = description },
            "jsonstring" => CoreVarDefs.JsonString with { Description = description },
            "any" => CoreVarDefs.Any with { Description = description },
            "promptlist" => CoreVarDefs.PromptList with { Description = description },
            "thinginfo" => ExtendVarDefs.ThingInfo with { Description = description },
            _ => CoreVarDefs.Any with { Description = description } // 未知类型，默认为 Any
        };
    }


    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec() =>
        this.ParseSpecs(isConsumed: true).Select(v => new ConsumedSpec(v.VarName, v.Def) { IsOptional = false }).ToList();


    /// <inheritdoc />
    public override List<ProducedSpec> GetProducedSpec() =>
        this.ParseSpecs(isConsumed: false).Select(v => new ProducedSpec(v.VarName, v.Def)).ToList();
}