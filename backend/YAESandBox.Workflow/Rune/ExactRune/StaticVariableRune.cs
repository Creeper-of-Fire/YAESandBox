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
        // 1. 解析脚本内容为键值对
        var parsedVariables = ScriptParser.Parse(this.Config.ScriptContent);

        // 2. 将解析结果转换为 object 类型的值，以符合 Context 的定义
        var contextPayload = parsedVariables.ToDictionary(kvp => kvp.Key, object? (kvp) => kvp.Value);
        
        // 3. 将打包好的 Context 设置到 Tuum 变量中
        tuumProcessorContent.SetTuumVar(this.Config.OutputContextName, contextPayload);
        
        // 4. 更新调试信息
        this.DebugDto.PackedContext = contextPayload;

        return Task.FromResult(Result.Ok());
    }

    /// <summary>
    /// 静态变量脚本符文的调试DTO。
    /// </summary>
    internal class StaticVariableRuneProcessorDebugDto : IRuneProcessorDebugDto
    {
        /// <summary>
        /// 在本次执行中成功打包并输出的 Context 内容。
        /// </summary>
        public Dictionary<string, object?> PackedContext { get; set; } = [];
    }
}

internal partial record StaticVariableRuneConfig
{
    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec() => [];

    /// <inheritdoc />
    public override List<ProducedSpec> GetProducedSpec() =>
    [
        // 明确声明此符文只产生一个名为 OutputContextName 的 Context 类型变量
        new(this.OutputContextName, CoreVarDefs.Context)
    ];
}

/// <summary>
/// “静态变量”符文的配置，用于定义一组静态字符串变量。
/// </summary>
[InFrontOf(typeof(PromptGenerationRuneConfig))] // 放在提示词生成符文的前面
[ClassLabel("🤔静态变量")]
internal partial record StaticVariableRuneConfig : AbstractRuneConfig<StaticVariableRuneProcessor>
{
    private const string DefaultOutputContextName = "Context";
    
    /// <summary>
    /// 输出的 Context 变量的名称。
    /// </summary>
    [Required]
    [DefaultValue(DefaultOutputContextName)]
    [Display(Name = "输出变量名", Description = "指定包含所有已定义变量的 Context 对象的名称。")]
    public string OutputContextName { get; init; } = DefaultOutputContextName;
    
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
    public string ScriptContent { get; init; } = string.Empty;

    /// <inheritdoc />
    protected override StaticVariableRuneProcessor ToCurrentRune(WorkflowRuntimeService workflowRuntimeService) =>
        new(workflowRuntimeService, this);
}