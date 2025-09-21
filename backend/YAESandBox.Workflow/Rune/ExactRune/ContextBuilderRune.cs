using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Tuum;
using YAESandBox.Workflow.VarSpec;

namespace YAESandBox.Workflow.Rune.ExactRune;

/// <summary>
/// 上下文构建符文的处理器。
/// 负责合并输入的多个Context，并打包其他独立的变量，最终生成一个新的Context。
/// </summary>
internal class ContextBuilderRuneProcessor(WorkflowRuntimeService workflowRuntimeService, ContextBuilderRuneConfig config)
    : INormalRune<ContextBuilderRuneConfig, ContextBuilderRuneProcessor.ContextBuilderRuneProcessorDebugDto>
{
    public ContextBuilderRuneConfig Config { get; } = config;
    public ContextBuilderRuneProcessorDebugDto DebugDto { get; } = new();

    public Task<Result> ExecuteAsync(TuumProcessor.TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
    {
        var finalContext = new Dictionary<string, object?>();

        // 1. 合并阶段 (Unpack & Merge)
        // 按照配置中定义的顺序进行合并，后面的会覆盖前面的同名键。
        foreach (var contextName in this.Config.ContextsToMerge)
        {
            var inputContext = tuumProcessorContent.GetTuumVar<Dictionary<string, object?>>(contextName);
                
            // 由于契约是严谨的 (IsOptional=false), 我们不再需要检查 null。
            // 如果变量不存在，GetTuumVar 会返回 null，但在强契约下，这本身就是一个流程错误，
            // 后续的操作自然会体现出来，或者我们可以在这里就提前失败。
            // 为稳健起见，还是加一个检查，以防上游流程有bug。
            if (inputContext == null)
            {
                // 在严谨模式下，这应该是一个错误，而不是一个警告。
                return Task.FromResult(Result.Fail($"必需的输入 Context '{contextName}' 未提供或类型不匹配。").ToResult());
            }
                
            foreach (var kvp in inputContext)
            {
                finalContext[kvp.Key] = kvp.Value;
            }
            this.DebugDto.AddLog($"成功合并了来自 '{contextName}' 的 {inputContext.Count} 个键值对。");
        }
        this.DebugDto.ContextAfterMerge = new Dictionary<string, object?>(finalContext);

        // 2. 打包阶段 (Pack)
        // 将独立的变量打包进Context，如果键已存在，则会覆盖。
        foreach (var variableName in this.Config.VariablesToPack)
        {
            // 同样，因为契约严谨，我们期望 GetTuumVar 一定能获取到值。
            var value = tuumProcessorContent.GetTuumVar(variableName);
                
            // 检查变量是否存在是GetTuumVar的责任，这里我们直接赋值。
            // 如果上游没有提供变量，value会是null，这会被忠实地打包进去。
            finalContext[variableName] = value;
            this.DebugDto.AddLog($"将变量 '{variableName}' 打包进 Context。");
        }
        
        // 3. 设置最终输出
        tuumProcessorContent.SetTuumVar(this.Config.OutputContextName, finalContext);
        this.DebugDto.FinalOutputContext = finalContext;

        return Task.FromResult(Result.Ok());
    }

    internal class ContextBuilderRuneProcessorDebugDto : IRuneProcessorDebugDto
    {
        public List<string> Logs { get; } = [];
        public Dictionary<string, object?>? ContextAfterMerge { get; internal set; }
        public Dictionary<string, object?>? FinalOutputContext { get; internal set; }
        public void AddLog(string log) => Logs.Add($"[{DateTime.UtcNow:HH:mm:ss.fff}] {log}");
    }
}

/// <summary>
/// “上下文构建”符文的配置。
/// </summary>
[ClassLabel("🏗️上下文构建")]
internal record ContextBuilderRuneConfig : AbstractRuneConfig<ContextBuilderRuneProcessor>
{
    private const string DefaultOutputContextName = "Context";

    /// <summary>
    /// 要合并的输入 Context 变量的名称列表。
    /// <para>这些变量是必需的。合并时，列表后面的 Context 会覆盖前面 Context 中的同名键。</para>
    /// </summary>
    [Display(Name = "合并的Context", Description = "指定一个或多个要合并的输入 Context 变量名。按顺序合并，后者覆盖前者。")]
    public List<string> ContextsToMerge { get; init; } = [];

    /// <summary>
    /// 要打包成 Context 的独立变量的名称列表。
    /// <para>这些变量是必需的，并会使用其原始名称作为在 Context 中的键。</para>
    /// <para>打包操作在合并之后进行，可能会覆盖合并后的值。</para>
    /// </summary>
    [Display(Name = "打包的变量", Description = "指定需要打包进 Context 的独立变量名。它们将使用自己的名字作为键。")]
    public List<string> VariablesToPack { get; init; } = [];

    /// <summary>
    /// 最终输出的 Context 变量的名称。
    /// </summary>
    [Required]
    [DefaultValue(DefaultOutputContextName)]
    [Display(Name = "输出变量名", Description = "指定最终构建完成的 Context 对象的名称。")]
    public string OutputContextName { get; init; } = DefaultOutputContextName;

    public override List<ConsumedSpec> GetConsumedSpec()
    {
        var specs = new List<ConsumedSpec>();

        // 声明要消费的 Contexts，它们都是必需的
        specs.AddRange(ContextsToMerge.Select(name =>
            new ConsumedSpec(name, CoreVarDefs.Context)));

        // 声明要消费的独立变量，它们也都是必需的
        // 使用 Any 类型，因为我们不关心打包前它们的具体类型
        specs.AddRange(VariablesToPack.Select(name =>
            new ConsumedSpec(name, CoreVarDefs.Any)));

        // 去重，以防用户在两个列表里填了相同的名字
        return specs.DistinctBy(s => s.Name).ToList();
    }

    public override List<ProducedSpec> GetProducedSpec() =>
        [new(this.OutputContextName, CoreVarDefs.Context)];

    protected override ContextBuilderRuneProcessor ToCurrentRune(WorkflowRuntimeService workflowRuntimeService) =>
        new(workflowRuntimeService, this);
}