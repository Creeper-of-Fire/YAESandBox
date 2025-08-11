using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.VarSpec;
using static YAESandBox.Workflow.Rune.ExactRune.StaticVariableRuneProcessor;
using static YAESandBox.Workflow.Tuum.TuumProcessor;

namespace YAESandBox.Workflow.Rune.ExactRune;

/// <summary>
/// 静态变量脚本符文处理器。
/// 在工作流运行时，执行脚本来定义多个字符串变量。
/// </summary>
/// <param name="workflowRuntimeService"><see cref="WorkflowRuntimeService"/></param>
/// <param name="config">符文配置。</param>
internal partial class StaticVariableRuneProcessor(WorkflowRuntimeService workflowRuntimeService, StaticVariableRuneConfig config)
    : INormalRune<StaticVariableRuneConfig, StaticVariableRuneProcessorDebugDto>
{
    private WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;

    /// <inheritdoc />
    public StaticVariableRuneConfig Config { get; init; } = config;

    /// <inheritdoc />
    public StaticVariableRuneProcessorDebugDto DebugDto { get; init; } = new();

    /// <summary>
    /// 执行脚本，将定义的变量注入到Tuum上下文中。
    /// </summary>
    public Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
    {
        // 所有复杂的解析逻辑都委托给了状态机解析器
        var parsedVariables = ScriptParser.Parse(Config.ScriptContent);

        foreach (var variable in parsedVariables)
        {
            tuumProcessorContent.SetTuumVar(variable.Key, variable.Value);
            DebugDto.DefinedVariables[variable.Key] = variable.Value;
        }

        return Task.FromResult(Result.Ok());
    }

    /// <summary>
    /// 静态变量脚本符文的调试DTO。
    /// </summary>
    internal class StaticVariableRuneProcessorDebugDto : IRuneProcessorDebugDto
    {
        /// <summary>
        /// 在本次执行中成功定义并注入的变量及其值。
        /// </summary>
        public Dictionary<string, string> DefinedVariables { get; } = [];
    }
}

internal partial record StaticVariableRuneConfig
{
    /// <summary>
    /// 用于运行时执行和静态分析的统一正则表达式。
    /// - `(?m)^(?!\s*#)`: 跳过注释行。
    /// - `... = \s*(?:...|...|...)`: 核心的值匹配部分是一个扁平的、按优先级排列的非捕获组。
    /// - 优先级: 三双引号 > 三单引号 > 单双引号 > 单单引号 > 无引号。
    /// </summary>
    [GeneratedRegex(
        pattern:
        @"(?m)^\s*(?!\s*#)\s*(?<name>[a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*(?:""""""(?<val_triple_double>(?:.|\n)*?)""""""|'''(?<val_triple_single>(?:.|\n)*?)'''|""(?<val_double>(?:\\""|[^""])*)""|'(?<val_single>(?:\\'|[^'])*)'|(?<val_unquoted>\S+))",
        options: RegexOptions.Compiled
    )]
    internal static partial Regex VariableDefinitionRegex();

    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec() => [];

    /// <inheritdoc />
    public override List<ProducedSpec> GetProducedSpec()
    {
        // 静态分析和运行时使用完全相同的、可预测的解析器
        var parsedVariables = ScriptParser.Parse(this.ScriptContent);

        var specs = parsedVariables
            .Select(kvp => new ProducedSpec(kvp.Key, CoreVarDefs.String))
            .DistinctBy(p => p.Name)
            .ToList();

        return specs;
    }
}

/// <summary>
/// “静态变量”符文的配置，用于定义一组静态字符串变量。
/// </summary>
[InFrontOf(typeof(PromptGenerationRuneConfig))] // 放在提示词生成符文的前面
[ClassLabel("🤔静态变量")]
internal partial record StaticVariableRuneConfig : AbstractRuneConfig<StaticVariableRuneProcessor>
{
    /// <summary>
    /// 定义变量的脚本内容。
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    [DataType(DataType.MultilineText)]
    [Display(
        Name = "变量定义脚本",
        Description = "定义变量。变量名支持中文。使用 '变量名 = \"值\"' 或用三引号定义多行字符串 '变量名 = \"\"\"多行值\"\"\"'。以 '#' 开头的行将被视为注释（多行变量中的除外）。",
        Prompt =
            """"
            示例：
            # 支持中文变量名
            角色名 = "小清姬"

            # 多行变量，其中'#'开头也不会被视作注释
            saying = """
            ### 个性：
            #### 喜欢的东西：
            “诚实”，这词汇是多么的美妙啊。我相信这是人创造的最完美的言语。
            #### 讨厌的东西：
            “谎言”，多么令人厌恶的词汇啊。最为糟糕的词汇，是我最讨厌的东西。
            """
            """"
    )]
    [DefaultValue("")]
    public required string ScriptContent { get; init; } = "";

    /// <inheritdoc />
    protected override StaticVariableRuneProcessor ToCurrentRune(WorkflowRuntimeService workflowRuntimeService) =>
        new(workflowRuntimeService, this);
}