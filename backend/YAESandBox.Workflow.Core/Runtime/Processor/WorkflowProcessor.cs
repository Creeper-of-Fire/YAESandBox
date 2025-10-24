using System.Collections.Concurrent;
using System.Collections.Immutable;
using YAESandBox.Depend.Results;
using YAESandBox.Workflow.Core.Config;
using YAESandBox.Workflow.Core.DebugDto;
using YAESandBox.Workflow.Core.Graph;
using YAESandBox.Workflow.Core.Runtime.InstanceId;
using YAESandBox.Workflow.Core.Runtime.WorkflowService;
using YAESandBox.Workflow.Core.Runtime.WorkflowService.Abstractions;

namespace YAESandBox.Workflow.Core.Runtime.Processor;

/// <summary>
/// 工作流的运行时。
/// </summary>
/// <param name="creatingContext"></param>
/// <param name="config"></param>
/// <param name="workflowInputs"></param>
public class WorkflowProcessor(
    WorkflowConfig config,
    ICreatingContext creatingContext,
    Dictionary<string, string> workflowInputs)
    : IProcessorWithDebugDto<IWorkflowProcessorDebugDto>
{
    /// <inheritdoc />
    public ProcessorContext ProcessorContext { get; } = creatingContext.ExtractContext();

    private WorkflowConfig Config { get; } = config;

    /// <summary>
    /// 工作流的数据存储区。
    /// Key 是一个端点（枢机ID + 端点名），Value 是该端点产生的数据。
    /// 这个存储区在工作流执行期间被动态填充。
    /// </summary>
    private Dictionary<TuumConnectionEndpoint, object?> WorkflowDataStore { get; } = [];

    private Dictionary<string, string> WorkflowInputs { get; } = workflowInputs;

    private Lock DataStoreLock { get; } = new();

    /// <summary>
    /// 缓存Tuum的运行时实例，以ConfigId为键，用于收集DebugDto。
    /// TODO 考虑和持久化一样，使用一个横切点来缓存DebugDto？这样的话，就不需要在这里缓存Processor了，而且现在持久化是会丢失DebugDto的。
    /// 
    /// </summary>
    private ConcurrentDictionary<string, TuumProcessor> TuumProcessors { get; } = new();

    /// <inheritdoc />
    public IWorkflowProcessorDebugDto DebugDto => new WorkflowProcessorDebugDto
    {
        TuumProcessorDebugDtos = this.TuumProcessors.Values.Select(p => p.DebugDto).ToList()
    };

    /// <inheritdoc />
    public record WorkflowProcessorDebugDto : IWorkflowProcessorDebugDto
    {
        /// <inheritdoc />
        public required IList<ITuumProcessorDebugDto> TuumProcessorDebugDtos { get; init; }
    }


    /// <summary>
    /// 执行工作流。
    /// </summary>
    public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(CancellationToken cancellationToken = default)
    {
        var persistenceService = this.ProcessorContext.RuntimeService.PersistenceService;
        var workflowInstanceId = this.ProcessorContext.InstanceId;

        // 工作流的输入是初始的 workflowInputs 字典
        // 工作流的输出是图执行器成功后返回的最终数据存储 (IDictionary<GraphConnectionEndpoint<string>, object?>)，尽管如此，它并没有真正的派上用场。
        var resultTask = persistenceService
            .WithPersistence(workflowInstanceId, this.WorkflowInputs)
            .ExecuteAsync(async currentInputs =>
            {
                // --- 1. 预处理阶段：构建完整的Tuum图 ---

                // a. 创建虚拟输入节点实例
                var virtualInputNode =
                    new VirtualWorkflowInputNode(this.Config, currentInputs.ToDictionary(kv => kv.Key, object? (kv) => kv.Value));

                // b. 将启用的Tuum转换为TuumGraphNode
                var enabledTuums = this.Config.Tuums.Where(t => t.Enabled).ToList();
                var tuumGraphNodes = enabledTuums.Select(t => new TuumGraphNode(t)).ToList();

                // c. 组合成完整的节点列表（用于自动连接）
                var allGraphNodes = new List<IGraphNode<string>> { virtualInputNode };
                allGraphNodes.AddRange(tuumGraphNodes);

                // --- 2. 确定连接关系 ---
                List<GraphConnection<string>> connections;

                if (this.Config.Graph?.Connections is null or { Count: 0 } || this.Config.Graph.EnableAutoConnect)
                {
                    var autoConnectResult = GraphExecutor.TryAutoConnect<IGraphNode<string>, string>(allGraphNodes);
                    if (autoConnectResult.TryGetError(out var autoConnectError, out var autoConnections))
                        return autoConnectError;
                    connections = autoConnections;
                }
                else
                {
                    // 手动连接
                    connections = this.Config.Graph.Connections.Select(c =>
                        new GraphConnection<string>(
                            new GraphConnectionEndpoint<string>(c.Source.TuumId, c.Source.EndpointName),
                            new GraphConnectionEndpoint<string>(c.Target.TuumId, c.Target.EndpointName)
                        )).ToList();
                }

                // --- 3. 执行阶段：调用通用执行器 ---

                // a. 准备初始数据
                var initialData = new Dictionary<GraphConnectionEndpoint<string>, object?>();
                var initialOutputs = virtualInputNode.ProduceOutputs();
                foreach ((string portName, object? value) in initialOutputs)
                {
                    initialData[new GraphConnectionEndpoint<string>(VirtualWorkflowInputNode.VIRTUAL_INPUT_ID, portName)] = value;
                }

                // b. 调用执行器
                var executionResult = await GraphExecutor.ExecuteAsync(
                    tuumGraphNodes, // 只执行真实的Tuum节点
                    connections,
                    initialData,
                    this.ExecuteTuumNodeAsync,
                    this.CloneAndSanitizeForFanOut,
                    cancellationToken);

                if (executionResult.TryGetError(out var executionError, out var executionOutputs))
                {
                    return executionError;
                }
                
                // 【关键的转换步骤】
                // 在返回之前，将运行时字典转换为持久化DTO
                var persistencePayload = new Dictionary<string, object?>();
                foreach ((var endpoint, object? value) in executionOutputs)
                {
                    // 使用我们之前为转换器设计的相同格式
                    string stringKey = $"{endpoint.NodeId}::{endpoint.PortName}";
                    persistencePayload[stringKey] = value;
                }

                return Result.Ok(persistencePayload);
            }).RunAsync();

        // 无论结果来自缓存还是新执行，都将其转换为最终的 WorkflowExecutionResult
        var finalResult = await resultTask;
        if (finalResult.TryGetError(out var error, out _))
        {
            return new WorkflowExecutionResult(false, error.ToDetailString(), "WorkflowExecutionFailed");
        }

        // 成功时，finalResult.Value 包含了所有节点的输出数据，这可能有用，但我们目前只返回成功状态
        return new WorkflowExecutionResult(true, null, null);
    }

    private async Task<Result<Dictionary<string, object?>>> ExecuteTuumNodeAsync(
        IGraphNode<string> node, IReadOnlyDictionary<string, object?> nodeInputs, CancellationToken ct)
    {
        if (node is not TuumGraphNode tuumNode)
        {
            // 虚拟节点不在此阶段执行
            return new Dictionary<string, object?>();
        }

        var tuumConfig = tuumNode.Config;
        // 目前 tuumNode 的 nodeId 是 Tuum的ConfigId，之后，在面对复杂情况时，我们需要通过更复杂的情况来生成nodeId，以便使得每个nodeId在那种情况下依旧是唯一的
        // 这种情况下，应该确保每个node都只执行一次
        string nodeId = node.Id;
        // --- 创建阶段 ---
        var tuumCreatingContext = this.ProcessorContext.CreateChildWithScope(nodeId);
        var tuumProcessor = tuumConfig.ToTuumProcessor(tuumCreatingContext);

        // 直接调用TuumProcessor的ExecuteAsync
        var tuumResult = await tuumProcessor.ExecuteAsync(nodeInputs, ct);

        // 将Result<Dictionary> 适配为图执行器需要的格式
        if (tuumResult.TryGetError(out var error, out var outputs))
        {
            return error;
        }

        return outputs;
    }

    /// <summary>
    /// 对要扇出的数据进行净化处理，防止并行任务中的意外修改。
    /// </summary>
    /// <param name="originalValue">原始数据对象。</param>
    /// <returns>一个被处理过的、更安全的数据副本。</returns>
    private object? CloneAndSanitizeForFanOut(object? originalValue)
    {
        if (originalValue is null)
        {
            return null;
        }

        // 对于常见集合类型，转换为不可变集合
        switch (originalValue)
        {
            case IList<object> list:
                return list.ToImmutableList();
            case IDictionary<object, object> dict:
                return dict.ToImmutableDictionary();
            case IList<string> stringList:
                return stringList.ToImmutableList();
            case IDictionary<string, object> stringDict:
                return stringDict.ToImmutableDictionary();
            // 可以根据需要在这里添加更多的集合类型转换
        }

        // 对于可克隆的 record 类型，执行浅克隆
        // C#没有一个统一的 IsRecord() 方法，但我们可以利用 ICloneable 接口
        // 如果你的 record 都实现了 ICloneable (with 表达式不会自动实现这个)
        // 一个更通用的方法是检查 with 表达式所需的方法，但这太复杂。
        // 最佳实践是让需要保护的自定义对象实现一个 Clone 方法。
        if (originalValue is ICloneable cloneable)
        {
            return cloneable.Clone();
        }

        var type = originalValue.GetType();

        // 对于值类型和字符串，它们本身是不可变的或按值传递的，直接返回
        if (type.IsValueType || originalValue is string)
        {
            return originalValue;
        }

        // 对于未知的引用类型，我们只能传递引用，并依赖开发者约定
        // 这里可以加一条警告日志，如果需要严格模式的话
        return originalValue;
    }
}