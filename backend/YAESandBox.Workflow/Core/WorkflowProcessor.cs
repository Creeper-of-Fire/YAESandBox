using System.Collections.Immutable;
using YAESandBox.Depend.Results;
using YAESandBox.Workflow.Core.Abstractions;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Tuum;
using YAESandBox.Workflow.Utility;

namespace YAESandBox.Workflow.Core;

public class WorkflowProcessor(
    WorkflowRuntimeService runtimeService,
    WorkflowConfig config,
    Dictionary<string, string> workflowInputs)
    : IProcessorWithDebugDto<IWorkflowProcessorDebugDto>
{
    private WorkflowConfig Config { get; } = config;

    /// <summary>
    /// 工作流的数据存储区。
    /// Key 是一个端点（枢机ID + 端点名），Value 是该端点产生的数据。
    /// 这个存储区在工作流执行期间被动态填充。
    /// </summary>
    private Dictionary<TuumConnectionEndpoint, object?> WorkflowDataStore { get; } = [];

    private Lock DataStoreLock { get; } = new();


    private WorkflowRuntimeService RuntimeService { get; } = runtimeService;
    private List<TuumProcessor> Tuums { get; } = config.Tuums.ConvertAll(it => it.ToTuumProcessor(runtimeService));

    /// <summary>
    /// 一个特殊的TuumId，用于表示工作流的入口。
    /// </summary>
    private const string WorkflowInputSourceId = "@workflow";

    /// <inheritdoc />
    public IWorkflowProcessorDebugDto DebugDto => new WorkflowProcessorDebugDto
    {
        TuumProcessorDebugDtos = this.Tuums.ConvertAll(it => it.DebugDto)
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
        // --- 0. 初始化数据存储区 ---
        // 将工作流的触发参数（入口）预加载到数据存储区中
        foreach (var param in workflowInputs)
        {
            var workflowInputEndpoint = new TuumConnectionEndpoint(WorkflowInputSourceId, param.Key);
            this.WorkflowDataStore[workflowInputEndpoint] = param.Value;
        }

        List<WorkflowConnection> effectiveConnections;
        // 如果连接未定义，则尝试自动连接
        if (this.Config.Connections is null)
        {
            var autoConnectResult = this.TryAutoConnect();
            if (autoConnectResult.TryGetError(out var error, out var generatedConnections))
            {
                // 自动连接失败，返回错误
                return new WorkflowExecutionResult(false, error.Message, "AutoConnectionFailed");
            }

            effectiveConnections = generatedConnections;
        }
        else
        {
            effectiveConnections = this.Config.Connections;
        }

        // --- 1. 依赖分析与执行计划生成 ---
        var nodes = this.Tuums.ToDictionary(tuum => tuum.TuumContent.TuumConfig.ConfigId, tuum => new ExecutionNode(tuum));

        // 根据显式连接构建依赖图
        foreach (var connection in effectiveConnections)
        {
            // 如果源或目标枢机不存在，则跳过（校验阶段会报告此错误）
            if (!nodes.TryGetValue(connection.Source.TuumId, out var sourceNode) ||
                !nodes.TryGetValue(connection.Target.TuumId, out var targetNode))
            {
                continue;
            }

            // 添加依赖关系：目标节点依赖于源节点
            targetNode.Dependencies.Add(sourceNode);
            sourceNode.Dependents.Add(targetNode);
        }

        // --- 2. 拓扑排序与并行执行 ---
        var completedNodes = new HashSet<ExecutionNode>();
        var readyToExecute = new Queue<ExecutionNode>(nodes.Values.Where(n => n.Dependencies.Count == 0));

        while (completedNodes.Count < nodes.Count)
        {
            if (readyToExecute.Count == 0)
            {
                string remainingNodeNames = string.Join(", ",
                    nodes.Values.Except(completedNodes).Select(n => n.Tuum.TuumContent.TuumConfig.ConfigId));
                return new WorkflowExecutionResult(false, $"工作流存在循环依赖或连接断裂，无法继续执行。剩余节点: {remainingNodeNames}", "CircularDependency");
            }

            var currentBatch = new List<ExecutionNode>();
            while (readyToExecute.TryDequeue(out var node))
            {
                currentBatch.Add(node);
            }

            var tasks = currentBatch.Select(node => this.ExecuteSingleTuumAsync(node,effectiveConnections, cancellationToken)).ToList();
            var results = await Task.WhenAll(tasks);

            // --- 3. 处理执行结果与更新依赖 ---
            foreach (var result in results)
            {
                if (result.TryGetError(out var error))
                {
                    // 任何一个并行枢机失败，则整个工作流失败
                    return new WorkflowExecutionResult(false, error.Message, "TuumExecutionFailed");
                }
            }

            foreach (var executedNode in currentBatch)
            {
                completedNodes.Add(executedNode);
                foreach (var dependentNode in executedNode.Dependents)
                {
                    // 检查这个后继者的所有依赖是否都已完成
                    if (dependentNode.Dependencies.All(completedNodes.Contains))
                    {
                        readyToExecute.Enqueue(dependentNode);
                    }
                }
            }
        }

        return new WorkflowExecutionResult(true, null, null);
    }

    /// <summary>
    /// 尝试基于命名约定自动生成工作流连接。
    /// </summary>
    /// <returns>成功时返回生成的连接列表，失败时返回错误信息。</returns>
    private Result<List<WorkflowConnection>> TryAutoConnect()
    {
        var generatedConnections = new List<WorkflowConnection>();
        var allAvailableOutputs = new Dictionary<string, TuumConnectionEndpoint>();

        // 1. 收集所有可用的输出源（工作流输入 + 所有祝祷的输出）
        // 首先添加工作流自身的输入作为顶级数据源
        foreach (string workflowInputName in this.Config.WorkflowInputs)
        {
            if (allAvailableOutputs.ContainsKey(workflowInputName))
            {
                return Result.Fail($"自动连接失败：输出端点名称 '{workflowInputName}' 在工作流输入中已存在，与其它输出源冲突。");
            }

            allAvailableOutputs.Add(workflowInputName, new TuumConnectionEndpoint(WorkflowInputSourceId, workflowInputName));
        }

        // 然后添加所有祝祷的输出端点
        foreach (var tuum in this.Tuums)
        {
            var tuumConfig = tuum.TuumContent.TuumConfig;
            // Tuum 的输出端点是 OutputMappings 的 Values 展平后的结果
            var tuumOutputEndpoints = tuumConfig.OutputMappings.Values.SelectMany(names => names).Distinct();

            foreach (string outputEndpointName in tuumOutputEndpoints)
            {
                if (allAvailableOutputs.TryGetValue(outputEndpointName, out var existingSource))
                {
                    return Result.Fail(
                        $"自动连接失败：存在多个名为 '{outputEndpointName}' 的输出端点，无法明确连接源。冲突发生在祝祷 '{tuumConfig.ConfigId}' 与 '{existingSource.TuumId}' 之间。");
                }

                allAvailableOutputs.Add(outputEndpointName, new TuumConnectionEndpoint(tuumConfig.ConfigId, outputEndpointName));
            }
        }

        // 2. 遍历所有祝祷的输入，并尝试为它们查找匹配的输出源
        foreach (var tuum in this.Tuums)
        {
            var tuumConfig = tuum.TuumContent.TuumConfig;
            // Tuum 的输入端点是 InputMappings 的 Keys
            foreach (string inputEndpointName in tuumConfig.InputMappings.Keys)
            {
                if (allAvailableOutputs.TryGetValue(inputEndpointName, out var sourceEndpoint))
                {
                    var targetEndpoint = new TuumConnectionEndpoint(tuumConfig.ConfigId, inputEndpointName);
                    generatedConnections.Add(new WorkflowConnection(sourceEndpoint, targetEndpoint));
                }
                else
                {
                    // 如果找不到匹配的源，则自动连接失败
                    return Result.Fail($"自动连接失败：祝祷 '{tuumConfig.ConfigId}' 的输入端点 '{inputEndpointName}' 找不到任何匹配的输出源。");
                }
            }
        }

        return Result.Ok(generatedConnections);
    }

    /// <summary>
    /// 辅助方法：执行单个枢机，包括准备输入和存储输出。
    /// </summary>
    private async Task<Result> ExecuteSingleTuumAsync(ExecutionNode node, List<WorkflowConnection> connections, CancellationToken cancellationToken)
    {
        var tuum = node.Tuum;
        string tuumId = tuum.TuumContent.TuumConfig.ConfigId;

        // 1. 准备输入：从数据存储区为当前枢机收集所有需要的输入数据
        var tuumInputs = new Dictionary<string, object?>();
        var connectionsToThisTuum = connections.Where(c => c.Target.TuumId == tuumId);

        foreach (var connection in connectionsToThisTuum)
        {
            // 从数据存储区查找源端点的值
            if (this.WorkflowDataStore.TryGetValue(connection.Source, out object? sourceValue))
            {
                tuumInputs[connection.Target.EndpointName] = sourceValue;
            }
            else
            {
                // 如果找不到源数据，则传递 null。校验阶段应捕获此问题。
                tuumInputs[connection.Target.EndpointName] = null;
            }
        }

        // 2. 执行枢机
        var tuumResult = await tuum.ExecuteAsync(tuumInputs, cancellationToken);
        if (tuumResult.TryGetError(out var error, out var tuumOutputs))
        {
            return error;
        }

        // 3. 存储输出：将枢机的输出结果经过净化处理后，存回工作流的数据存储区
        lock (this.DataStoreLock)
        {
            foreach (var output in tuumOutputs)
            {
                var outputEndpoint = new TuumConnectionEndpoint(tuumId, output.Key);
                // 对输出值进行净化（克隆/转不可变），以保证数据隔离
                this.WorkflowDataStore[outputEndpoint] = this.CloneAndSanitizeForFanOut(output.Value);
            }
        }

        return Result.Ok();
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

    /// <summary>
    /// 辅助类：用于构建依赖图的节点
    /// </summary>
    private class ExecutionNode(TuumProcessor tuum)
    {
        public TuumProcessor Tuum { get; } = tuum;
        public HashSet<ExecutionNode> Dependencies { get; } = []; // 它依赖谁
        public HashSet<ExecutionNode> Dependents { get; } = []; // 谁依赖它
    }
}