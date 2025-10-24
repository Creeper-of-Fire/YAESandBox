using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.ResultsExtend;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.Core.Config.RuneConfig;
using YAESandBox.Workflow.Core.Config.Stored;
using YAESandBox.Workflow.Core.DebugDto;
using YAESandBox.Workflow.Core.Runtime.Processor;
using YAESandBox.Workflow.Core.Runtime.Processor.RuneProcessor;
using YAESandBox.Workflow.Core.VarSpec;
using YAESandBox.Workflow.Schema;
using static YAESandBox.Workflow.Core.Runtime.Processor.TuumProcessor;

namespace YAESandBox.Workflow.ExactRune;

/// <summary>
/// "引用符文"的运行时处理器。
/// 它是一个无状态的代理。每次执行时，它都会动态地查找并创建被引用的符文处理器，然后将调用委托给它。
/// </summary>
internal class ReferenceRuneProcessor(ReferenceRuneConfig config, ICreatingContext creatingContext)
    : NormalRuneProcessor<ReferenceRuneConfig, ReferenceRuneProcessor.ReferenceRuneDebugDto>(config, creatingContext)
{
    public override ReferenceRuneDebugDto DebugDto { get; } = new()
    {
        TargetRefId = config.TargetRuneRef?.RefId,
        TargetVersion = config.TargetRuneRef?.Version
    };

    public override async Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent,
        CancellationToken cancellationToken = default)
    {
        // 1. 每次执行时都动态解析和创建 Processor
        var creationResult = await this.CreateActualProcessorAsync();

        if (creationResult.TryGetError(out var error, out var actualProcessor))
        {
            this.DebugDto.RuntimeError = $"创建引用的符文处理器失败: {error.Message}";
            return Result.Fail(error);
        }

        this.DebugDto.ResolvedRuneType = actualProcessor.Config.RuneType;
        this.DebugDto.ResolvedRuneConfigId = actualProcessor.Config.ConfigId;

        // 2. 将执行完全委托给新创建的内部处理器
        var result = await actualProcessor.ExecuteAsync(tuumProcessorContent, cancellationToken);

        // 3. 将内部处理器的调试信息代理到我们自己的调试DTO中
        this.DebugDto.ActualRuneDebugDto = actualProcessor.DebugDto;
        if (result.TryGetError(out var actualRuntimeError))
        {
            this.DebugDto.RuntimeError = actualRuntimeError.ToDetailString();
        }

        return result;
    }

    /// <summary>
    /// 异步查找并创建被引用的符文处理器。
    /// </summary>
    private async Task<Result<IRuneProcessor<AbstractRuneConfig, IRuneProcessorDebugDto>>> CreateActualProcessorAsync()
    {
        if (this.Config.TargetRuneRef is null)
        {
            return NormalError.ValidationError("引用符文的 TargetRuneRef 未配置。");
        }

        // 从运行时服务中获取查找服务和用户ID
        var runtimeService = this.ProcessorContext.RuntimeService;
        var findService = runtimeService.FindService;
        string userId = runtimeService.UserId;

        // 异步查找配置
        var findResult = await findService.FindRuneConfigByRefAsync(userId, this.Config.TargetRuneRef);

        if (findResult.TryGetError(out var findResultError, out var storedConfig))
        {
            return findResultError;
        }

        // 使用找到的配置创建真正的处理器
        // 注意：为被引用的符文创建一个新的、隔离的子上下文，这很重要。
        // 我们使用自己的 ConfigId 作为作用域名称，以表明这是 "ReferenceRune" 内部的一次执行。
        var childCreatingContext = this.ProcessorContext.CreateContextForChild(Guid.NewGuid());
        var actualProcessor = storedConfig.Content.ToRuneProcessor(childCreatingContext);

        return Result.Ok(actualProcessor);
    }

    internal record ReferenceRuneDebugDto : IRuneProcessorDebugDto
    {
        public string? TargetRefId { get; init; }
        public string? TargetVersion { get; init; }
        public string? ResolvedRuneType { get; set; }
        public string? ResolvedRuneConfigId { get; set; }
        public IRuneProcessorDebugDto? ActualRuneDebugDto { get; set; }
        public string? RuntimeError { get; set; }
    }
}

/// <summary>
/// "引用符文"的配置。
/// 它本身不包含任何逻辑，而是通过 RefId 和 Version 引用一个已保存的全局符文。
/// </summary>
[ClassLabel("引用符文", Icon = "🔗")]
[RuneCategory("工作流控制")]
internal record ReferenceRuneConfig : AbstractRuneConfig<ReferenceRuneProcessor>
{
    [Required]
    [Display(Name = "引用的全局符文", Description = "选择一个要在此处引用的全局符文配置。")]
    public StoredConfigRef? TargetRuneRef { get; init; }

    // TODO: [技术债] 架构演进 - 静态分析缓存
    // 当前的缓存机制（方案A）是一个为了快速实现而采取的务实策略。它通过让 RuneAnalysisService 修改
    // 此配置对象的状态，使得 TuumAnalysisService 可以保持同步和简单。
    // 这种方法的缺点是：
    // 1. 破坏了配置对象的不可变性。
    // 2. 引入了隐式的执行顺序依赖（必须先分析符文，再分析枢机）。
    // 理想的最终形态（方案B）是：
    // 1. 移除这里的缓存属性，使 ReferenceRuneConfig 恢复为纯粹的数据载体。
    // 2. 将 TuumAnalysisService 重构为异步服务 (AnalyzeAsync)。
    // 3. 在 TuumAnalysisService 内部，调用 RuneAnalysisService 并解析所有引用符文，从而实现逻辑的完全内聚。
    // 当未来有重构资源时，应考虑向方案B演进。

    #region Analysis Cache

    /// <summary>
    /// (非持久化) 缓存上次分析时解析出的真实输入规格。
    /// </summary>
    [JsonIgnore]
    public List<ConsumedSpec>? CachedConsumedSpecs { get; private set; }

    /// <summary>
    /// (非持久化) 缓存上次分析时解析出的真实输出规格。
    /// </summary>
    [JsonIgnore]
    public List<ProducedSpec>? CachedProducedSpecs { get; private set; }

    /// <summary>
    /// (内部使用) 由分析服务调用，用于更新此实例的缓存。
    /// </summary>
    internal void UpdateAnalysisCache(List<ConsumedSpec> consumed, List<ProducedSpec> produced)
    {
        this.CachedConsumedSpecs = consumed;
        this.CachedProducedSpecs = produced;
    }

    /// <summary>
    /// (内部使用) 清除分析缓存。
    /// </summary>
    internal void ClearAnalysisCache()
    {
        this.CachedConsumedSpecs = null;
        this.CachedProducedSpecs = null;
    }

    #endregion

    #region Static Analysis Delegation

    public override List<ConsumedSpec> GetConsumedSpec() => this.CachedConsumedSpecs ?? [];
    public override List<ProducedSpec> GetProducedSpec() => this.CachedProducedSpecs ?? [];

    #endregion

    protected override ReferenceRuneProcessor ToCurrentRune(ICreatingContext creatingContext) => new(this, creatingContext);
}