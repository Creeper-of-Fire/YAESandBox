using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Config.RuneConfig;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Runtime.Processor;
using YAESandBox.Workflow.Runtime.Processor.RuneProcessor;
using YAESandBox.Workflow.VarSpec;
using static YAESandBox.Workflow.Runtime.Processor.TuumProcessor;
using Tomlyn;
using Tomlyn.Model;

namespace YAESandBox.Workflow.ExactRune;

/// <summary>
/// “静态变量”符文的运行时处理器。
/// 使用 Tomlyn 解析 TOML 格式的脚本，并将结构化数据注入到 Tuum 上下文中。
/// </summary>
/// <param name="creatingContext"></param>
/// <param name="config">符文配置。</param>
internal class StaticVariableRuneProcessor(StaticVariableRuneConfig config, ICreatingContext creatingContext)
    : NormalRuneProcessor<StaticVariableRuneConfig, StaticVariableRuneProcessor.StaticVariableRuneProcessorDebugDto>(config,
        creatingContext)
{
    /// <summary>
    /// 解析 TOML 脚本，并将定义的变量注入到 Tuum 上下文中。
    /// </summary>
    /// <inheritdoc />
    public override Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(this.Config.ScriptContent))
        {
            return Result.Ok().AsCompletedTask();
        }

        try
        {
            // 1. 使用 Tomlyn 解析 TOML 脚本
            var model = Toml.ToModel(this.Config.ScriptContent);
            this.DebugDto.RawTomlModel = model.ToString(); // 记录解析后的模型快照

            // 2. 遍历顶层键值对
            foreach (var key in model.Keys)
            {
                var tomlObject = model[key];

                // 3. 将 TomlObject 转换为 .NET 原生对象/字典/列表
                object? runtimeValue = ConvertTomlObjectToRuntimeValue(tomlObject);

                // 4. 将转换后的值设置到 Tuum 变量池中
                tuumProcessorContent.SetTuumVar(key, runtimeValue);

                // 5. 更新调试信息
                this.DebugDto.DefinedVariables[key] = runtimeValue;
            }

            return Result.Ok().AsCompletedTask();
        }
        catch (Exception ex)
        {
            var error = new Error("TOML 脚本解析失败。", ex);
            this.DebugDto.ParsingError = error.ToDetailString();
            return Result.Fail(error).AsCompletedTask();
        }
    }

    /// <summary>
    /// 递归地将 TomlObject 转换为适合在 Tuum 中存储的运行时对象。
    /// TomlTable -> Dictionary &lt;string, object&gt;
    /// TomlTableArray -> List&lt;Dictionary&lt;string, object&gt;&gt;
    /// Primitive -> .NET primitive type
    /// </summary>
    private static object? ConvertTomlObjectToRuntimeValue(object tomlObject)
    {
        return tomlObject switch
        {
            // 表 -> 字典
            TomlTable table => table.ToDictionary(
                kvp => kvp.Key,
                kvp => ConvertTomlObjectToRuntimeValue(kvp.Value)),

            // 表数组 -> 字典列表
            TomlTableArray tableArray => tableArray
                .Select(ConvertTomlObjectToRuntimeValue)
                .ToList(),

            // 数组 -> 列表
            TomlArray array => array
                .Select(o => ConvertTomlObjectToRuntimeValue(o))
                .ToList(),

            // 其他所有基础类型，Tomlyn 已经为我们转换好了 (long, double, bool, string, etc.)
            _ => tomlObject
        };
    }

    /// <summary>
    /// 静态变量脚本符文的调试DTO。
    /// </summary>
    internal record StaticVariableRuneProcessorDebugDto : IRuneProcessorDebugDto
    {
        /// <summary>
        /// 解析 TOML 脚本时发生的错误（如果有）。
        /// </summary>
        public string? ParsingError { get; set; }

        /// <summary>
        /// Tomlyn 解析后得到的原始 TOML 模型（字符串表示）。
        /// </summary>
        public string? RawTomlModel { get; set; }

        /// <summary>
        /// 在本次执行中成功定义并注入的变量及其运行时值。
        /// </summary>
        public Dictionary<string, object?> DefinedVariables { get; } = [];
    }
}

internal partial record StaticVariableRuneConfig
{
    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec() => [];

    /// <inheritdoc />
    public override List<ProducedSpec> GetProducedSpec()
    {
        if (string.IsNullOrWhiteSpace(this.ScriptContent))
            return [];

        try
        {
            // 1. 使用 Tomlyn 解析脚本内容
            var model = Toml.ToModel(this.ScriptContent);
            var specs = new List<ProducedSpec>();

            // 2. 遍历 TOML 模型的顶层键
            foreach (string key in model.Keys)
            {
                object tomlObject = model[key];
                // 3. 递归地将 TOML 对象转换为 VarSpecDef
                var varDef = ConvertTomlObjectToVarSpecDef(tomlObject);
                specs.Add(new ProducedSpec(key, varDef));
            }

            return specs;
        }
        catch (Exception)
        {
            // 解析失败，返回空列表或错误标记
            return [];
        }
    }

    // 辅助方法：将 TomlObject 递归转换为 VarSpecDef
    private static VarSpecDef ConvertTomlObjectToVarSpecDef(object tomlObject)
    {
        switch (tomlObject)
        {
            case TomlTable table:
                var properties = table.ToDictionary(
                    kvp => kvp.Key,
                    kvp => ConvertTomlObjectToVarSpecDef(kvp.Value)
                );

                // TOML 表 -> RecordVarSpecDef
                return CoreVarDefs.RecordStringAny with
                {
                    Properties = properties
                };

            case TomlTableArray tableArray:
                var elementDef = tableArray.Count == 0
                    ? CoreVarDefs.Any // 空列表，元素类型未知，设为 Any
                    : ConvertTomlObjectToVarSpecDef(tableArray[0]);

                return CoreVarDefs.AnyList with { ElementDef = elementDef };

            // TOML 基础类型 -> PrimitiveVarSpecDef
            case string: return CoreVarDefs.String;
            case long:
            case int:
            case double: return CoreVarDefs.Number;
            case bool: return CoreVarDefs.Boolean;

            case TomlArray tomlArray:
                VarSpecDef arrayElementDef;
                if (tomlArray.Count == 0)
                {
                    arrayElementDef = CoreVarDefs.Any;
                }
                else
                {
                    // 推断数组的统一类型，这里简化为只看第一个元素
                    arrayElementDef = tomlArray[0] is not { } tomlArrayFirstItem
                        ? CoreVarDefs.Any
                        : ConvertTomlObjectToVarSpecDef(tomlArrayFirstItem);
                }

                return new ListVarSpecDef(
                    $"{arrayElementDef.TypeName}[]", // e.g., "String[]"
                    null,
                    arrayElementDef
                );

            // 其他情况或数组等，可以进一步细化
            default: return CoreVarDefs.Any;
        }
    }
}

/// <summary>
/// “静态变量”符文的配置，用于通过 TOML 定义一组结构化的静态变量。
/// </summary>
[InFrontOf(typeof(PromptGenerationRuneConfig))] // 放在提示词生成符文的前面
[ClassLabel("🤔静态变量")]
internal partial record StaticVariableRuneConfig : AbstractRuneConfig<StaticVariableRuneProcessor>
{
    /// <summary>
    /// 定义变量的 TOML 脚本内容。
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    [DataType(DataType.MultilineText)]
    [RenderWithMonacoEditor("toml")]
    [Display(
        Name = "TOML 变量定义",
        Description = "使用 TOML 格式定义变量。支持字符串、数字、布尔、数组和嵌套对象。若要使用中文或特殊字符作为变量名，请用双引号将其括起来。",
        Prompt =
            """"
            # 这是一个 TOML 示例，它是一种键值对配置文件，类似于ini。
            # 这是乱七八糟的注释。只支持单行注释。
            # 使用中文作为键的话，键必须被双引号包裹。
            "角色名" = "小清姬"

            ["个性"]
            "喜欢的东西" = "“诚实”，这词汇是多么的美妙啊。我相信这是人创造的最完美的言语。"
            "讨厌的东西" = "“谎言”，多么令人厌恶的词汇啊。最为糟糕的词汇，是我最讨厌的东西。"
            "技能介绍" = """
            这是一个多行字符串。
            它是多行的。
            并且可以用这些方式忽略无关的空白。  \
            """
            "对Master的态度" = '''
            这是字面量字符串，\n它不会被转义。
            '''

            [database]
            enabled = true
            ports = [ 8000, 8001, 8002 ]
            data = [ ["delta", "phi"], [3.14] ]
            temp_targets = { cpu = 79.5, case = 72.0 }

            # 表数组
            [[itemList]]
            "名称" = "治疗药水"
            "数量" = 5

            [[itemList]]
            "名称" = "魔法卷轴"
            "效果" = "火球术"
            """"
    )]
    [DefaultValue("")]
    public string ScriptContent { get; init; } = string.Empty;

    /// <inheritdoc />
    protected override StaticVariableRuneProcessor ToCurrentRune(ICreatingContext creatingContext) => new(this, creatingContext);
}