using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.Core.Config;
using YAESandBox.Workflow.Core.Config.RuneConfig;
using YAESandBox.Workflow.Core.DebugDto;
using YAESandBox.Workflow.Core.Runtime.InstanceId;
using YAESandBox.Workflow.Core.Runtime.Processor;
using YAESandBox.Workflow.Core.Runtime.Processor.RuneProcessor;
using YAESandBox.Workflow.Core.VarSpec;
using YAESandBox.Workflow.WorkflowService.Analysis;
using static YAESandBox.Workflow.Core.Runtime.Processor.TuumProcessor;
using static YAESandBox.Workflow.ExactRune.TuumRuneProcessor;

namespace YAESandBox.Workflow.ExactRune;

/// <summary>
/// “枢机符文”的运行时处理器。它将一个完整的枢机（Tuum）封装并作为一个独立的符文来执行。
/// </summary>
/// <param name="creatingContext"></param>
/// <param name="config">枢机符文的配置。</param>
internal class TuumRuneProcessor(TuumRuneConfig config, ICreatingContext creatingContext)
    : NormalRuneProcessor<TuumRuneConfig, TuumRuneProcessorDebugDto>(config, creatingContext)
{
    /// <summary>
    /// 枢机符文的调试信息。
    /// </summary>
    internal record TuumRuneProcessorDebugDto : IRuneProcessorDebugDto
    {
        /// <summary>
        /// 内部枢机执行后的调试快照。
        /// 如果为 null，表示内部枢机尚未执行。
        /// </summary>
        public ITuumProcessorDebugDto? InnerTuumDebugInfo { get; internal set; }
    }

    /// <summary>
    /// 执行封装的枢机。
    /// </summary>
    /// <inheritdoc />
    public override async Task<Result> ExecuteAsync(TuumProcessorContent outerTuumContent, CancellationToken cancellationToken = default)
    {
        // 1. 创建内部枢机的运行时处理器
        var innerTuumConfig = this.Config.InnerTuum;
        var innerTuumCreatingContext = this.ProcessorContext.CreateChildWithScope(innerTuumConfig.ConfigId);
        var innerTuumProcessor = innerTuumConfig.ToTuumProcessor(innerTuumCreatingContext);

        // 2. 准备内部枢机的输入
        // 枢机符文所消费的变量，就是其内部枢机的输入端点。
        var innerTuumInputs = new Dictionary<string, object?>();
        foreach (var consumedSpec in this.Config.GetConsumedSpec())
        {
            // 从外部枢机的变量池中，为内部枢机的输入端点获取数据。
            innerTuumInputs[consumedSpec.Name] = outerTuumContent.GetTuumVar(consumedSpec.Name);
        }

        // 3. 执行内部枢机
        var result = await innerTuumProcessor.ExecuteAsync(innerTuumInputs, cancellationToken);

        // 捕获内部枢机的调试信息
        this.DebugDto.InnerTuumDebugInfo = innerTuumProcessor.DebugDto;

        if (result.TryGetError(out var error, out var innerTuumOutputs))
        {
            // 如果子流程失败，则整个符文失败
            return error;
        }

        // 4. 处理内部枢机的输出
        // 内部枢机的输出端点，就是枢机符文生产的变量。
        foreach ((string outputEndpointName, object? outputValue) in innerTuumOutputs)
        {
            // 将内部枢机的输出，设置到外部枢机的变量池中。
            outerTuumContent.SetTuumVar(outputEndpointName, outputValue);
        }

        return Result.Ok();
    }
}

/// <summary>
/// “枢机符文”的配置，它封装了一个完整的枢机配置，使其可以像一个普通符文一样被使用。
/// </summary>
[ClassLabel("子枢机", Icon = "📦")]
internal record TuumRuneConfig : AbstractRuneConfig<TuumRuneProcessor>, IHasInnerTuumConfig
{
    /// <summary>
    /// 被封装在符文内部的枢机配置。
    /// </summary>
    [Required]
    [HiddenInForm(true)]
    [Display(Name = "子枢机配置", Description = "点击以配置此符文内部封装的子枢机流程。")]
    public TuumConfig InnerTuum { get; init; } = new()
    {
        // 为新创建的子枢机提供一个默认的、唯一的ConfigId
        ConfigId = Guid.NewGuid().ToString("N")
    };

    private static TuumAnalysisService TuumAnalysisService { get; } = new();

    private TuumAnalysisResult AnalysisResult => TuumAnalysisService.Analyze(this.InnerTuum);

    /// <summary>
    /// 获取此符文消费的变量。
    /// <para>这些变量直接来自于其内部枢机分析后得出的【输入端点】。</para>
    /// </summary>
    public override List<ConsumedSpec> GetConsumedSpec()
    {
        // TuumRune 的输入变量，就是其内部 Tuum 的输入端点。
        return this.AnalysisResult.ConsumedEndpoints;
    }

    /// <summary>
    /// 获取此符文生产的变量。
    /// <para>这些变量直接来自于其内部枢机分析后得出的【输出端点】。</para>
    /// </summary>
    public override List<ProducedSpec> GetProducedSpec()
    {
        // TuumRune 的输出变量，就是其内部 Tuum 的输出端点。
        return this.AnalysisResult.ProducedEndpoints;
    }

    /// <inheritdoc />
    protected override TuumRuneProcessor ToCurrentRune(ICreatingContext creatingContext) => new(this, creatingContext);

    /// <inheritdoc />
    public override List<EmittedEventSpec> AnalyzeEmittedEvents(IReadOnlyDictionary<string, VarSpecDef> resolvedVariableTypes) =>
        this.AnalysisResult.EmittedEvents;
}