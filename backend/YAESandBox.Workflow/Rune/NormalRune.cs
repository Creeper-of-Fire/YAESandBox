using YAESandBox.Depend.Results;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Rune.Config;
using YAESandBox.Workflow.Rune.Interface;
using YAESandBox.Workflow.Runtime;
using static YAESandBox.Workflow.Tuum.TuumProcessor;

namespace YAESandBox.Workflow.Rune;

/// <summary>
/// 普通的符文
/// </summary>
/// <typeparam name="TConfig"></typeparam>
/// <typeparam name="TDebug"></typeparam>
public abstract class NormalRune<TConfig, TDebug>(TConfig config, ICreatingContext creatingContext)
    : INormalRune<TConfig, TDebug>
    where TConfig : AbstractRuneConfig where TDebug : IRuneProcessorDebugDto, new()
{
    /// <inheritdoc />
    public virtual TDebug DebugDto { get; init; } = new();

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