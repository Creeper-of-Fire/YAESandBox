using YAESandBox.Depend.Results;
using YAESandBox.Workflow.Core.Config.RuneConfig;
using YAESandBox.Workflow.Core.DebugDto;
using YAESandBox.Workflow.Core.Runtime.WorkflowService;
using static YAESandBox.Workflow.Core.Runtime.Processor.TuumProcessor;

namespace YAESandBox.Workflow.Core.Runtime.Processor.RuneProcessor;

/// <summary>
/// 普通的符文
/// </summary>
/// <typeparam name="TConfig"></typeparam>
/// <typeparam name="TDebug"></typeparam>
public abstract class NormalRuneProcessor<TConfig, TDebug>(TConfig config, ICreatingContext creatingContext) : IRuneProcessor<TConfig, TDebug>
    where TConfig : AbstractRuneConfig where TDebug : IRuneProcessorDebugDto, new()
{
    /// <inheritdoc />
    public virtual TDebug DebugDto { get; } = new();

    /// <inheritdoc />
    public virtual TConfig Config { get; } = config;

    /// <inheritdoc />
    public virtual ProcessorContext ProcessorContext { get; } = creatingContext.ExtractContext();

    /// <summary>
    /// 工作流的运行时服务
    /// </summary>
    protected WorkflowRuntimeService WorkflowRuntimeService => this.ProcessorContext.RuntimeService;

    /// <inheritdoc />
    public abstract Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default);
}