using System.Collections.Concurrent;
using YAESandBox.Depend.Results;

namespace YAESandBox.Workflow.Core.Graph;

/// <summary>
/// 定义一个可执行图中的通用节点。
/// TNodeId 必须是可比较的，通常是 string 或 Guid。
/// </summary>
public interface IGraphNode<out TNodeId> where TNodeId : notnull
{
    /// <summary>
    ///  节点的唯一标识符。
    /// </summary>
    TNodeId Id { get; }

    /// <summary>
    /// 节点声明其消费的所有输入端口的名称。
    /// </summary>
    IEnumerable<string> GetInputPortNames();

    /// <summary>
    /// 节点声明其生产的所有输出端口的名称。
    /// </summary>
    IEnumerable<string> GetOutputPortNames();
}

/// <summary>
/// 定义一个通用连接的端点。
/// </summary>
public record GraphConnectionEndpoint<TNodeId>(TNodeId NodeId, string PortName) where TNodeId : notnull;

/// <summary>
/// 定义两个节点端口之间的通用连接。
/// </summary>
public record GraphConnection<TNodeId>(GraphConnectionEndpoint<TNodeId> Source, GraphConnectionEndpoint<TNodeId> Target)
    where TNodeId : notnull;

/// <summary>
/// 定义了一个通用的连接。
/// </summary>
/// <param name="SourceNodeId">源节点的ID。</param>
/// <param name="SourcePortName">源节点上的输出端口名称。</param>
/// <param name="TargetNodeId">目标节点的ID。</param>
/// <param name="TargetPortName">目标节点上的输入端口名称。</param>
public record GraphConnection(string SourceNodeId, string SourcePortName, string TargetNodeId, string TargetPortName);

/// <summary>
/// 表示图中两个节点之间的有向连接。
/// </summary>
/// <typeparam name="TNode">节点的类型。</typeparam>
/// <param name="Source">源节点。</param>
/// <param name="SourcePort">源节点上的输出端口名。</param>
/// <param name="Target">目标节点。</param>
/// <param name="TargetPort">目标节点上的输入端口名。</param>
public record Connection<TNode>(TNode Source, string SourcePort, TNode Target, string TargetPort);

/// <summary>
/// 一个通用的、基于拓扑排序的图执行引擎。
/// 它还提供了基于命名约定的自动连接功能。
/// </summary>
public static class GraphExecutor
{
    /// <summary>
    /// 根据节点的线性顺序，尝试基于命名约定自动生成连接。
    /// 这个版本严格遵守执行顺序，并能正确处理同名端口的“透传”情况。
    /// </summary>
    /// <typeparam name="TNode">节点的类型。</typeparam>
    /// <typeparam name="TNodeId">节点ID的类型。</typeparam>
    /// <param name="nodesInOrder">所有待连接的节点，必须按执行顺序排列。</param>
    /// <returns>成功时返回生成的连接列表，失败时返回错误信息。</returns>
    public static Result<List<GraphConnection<TNodeId>>> TryAutoConnect<TNode, TNodeId>(
        IReadOnlyList<TNode> nodesInOrder)
        where TNode : IGraphNode<TNodeId>
        where TNodeId : notnull
    {
        var generatedConnections = new List<GraphConnection<TNodeId>>();

        // Key: PortName, Value: 最新产生该端口的源端点
        var availableOutputs = new Dictionary<string, GraphConnectionEndpoint<TNodeId>>();

        // 严格按照线性顺序处理每个节点
        foreach (var currentNode in nodesInOrder)
        {
            var inputPorts = currentNode.GetInputPortNames().ToHashSet();
            var outputPorts = currentNode.GetOutputPortNames();

            // 1. 为当前节点的输入端口查找源
            foreach (var inputPortName in inputPorts)
            {
                if (availableOutputs.TryGetValue(inputPortName, out var sourceEndpoint))
                {
                    var targetEndpoint = new GraphConnectionEndpoint<TNodeId>(currentNode.Id, inputPortName);
                    generatedConnections.Add(new GraphConnection<TNodeId>(sourceEndpoint, targetEndpoint));
                }
                else
                {
                    // 找不到源，自动连接失败
                    return Result.Fail($"自动连接失败：节点 '{currentNode.Id}' 的输入端口 '{inputPortName}' 在其执行前找不到任何匹配的输出源。");
                }
            }

            // 2. 将当前节点的输出端口更新为新的可用源，并进行覆盖检查
            foreach (var outputPortName in outputPorts)
            {
                // **核心规则：检查是否存在非法覆盖**
                // 条件1: 该输出端口名在之前已经存在 (availableOutputs.ContainsKey)
                // 条件2: 当前节点并没有消费这个端口 ( !inputPorts.Contains )
                if (availableOutputs.TryGetValue(outputPortName, out var existingSource) &&
                    !inputPorts.Contains(outputPortName))
                {
                    // 检测到非法覆盖，自动连接失败
                    return Result.Fail(
                        $"自动连接失败：节点 '{currentNode.Id}' 的输出端口 '{outputPortName}' " +
                        $"隐式覆盖了由节点 '{existingSource.NodeId}' 产生的同名端口。" +
                        $"如果要更新一个值，请确保该节点也消费了同名输入。");
                }

                // 合法的“更新”或“全新生产”，更新可用输出表
                availableOutputs[outputPortName] = new GraphConnectionEndpoint<TNodeId>(currentNode.Id, outputPortName);
            }
        }

        return Result.Ok(generatedConnections);
    }

    /// <summary>
    /// 根据给定的节点和连接，以拓扑排序的方式并行执行一个有向无环图 (DAG)。
    /// </summary>
    /// <typeparam name="TNode">节点的类型。</typeparam>
    /// <typeparam name="TNodeId">节点ID的类型。</typeparam>
    /// <param name="nodes">图中所有节点的集合。</param>
    /// <param name="connections">定义节点间数据流的连接列表。</param>
    /// <param name="initialData">图执行开始前，预置的初始数据。Key是端点，Value是数据。</param>
    /// <param name="executeNodeAsync">一个委托，定义了如何执行单个节点。它接收节点本身和其输入数据，并返回其输出数据。</param>
    /// <param name="postProcessOutput">一个委托，用于在存储节点输出之前对其进行处理。
    /// 主要用于克隆或净化数据，以防止在扇出场景中发生数据污染。
    /// 如果为 null，则不进行任何处理。</param>
    /// <param name="cancellationToken">用于取消操作的CancellationToken。</param>
    /// <returns>执行完成后，整个图的数据存储区。</returns>
    public static async Task<Result<ConcurrentDictionary<GraphConnectionEndpoint<TNodeId>, object?>>> ExecuteAsync<TNode, TNodeId>(
        IEnumerable<TNode> nodes,
        IEnumerable<GraphConnection<TNodeId>> connections,
        IReadOnlyDictionary<GraphConnectionEndpoint<TNodeId>, object?> initialData,
        Func<TNode, IReadOnlyDictionary<string, object?>, CancellationToken, Task<Result<Dictionary<string, object?>>>> executeNodeAsync,
        Func<object?, object?>? postProcessOutput = null,
        CancellationToken cancellationToken = default)
        where TNode : IGraphNode<TNodeId>
        where TNodeId : notnull
    {
        var dataStore = new ConcurrentDictionary<GraphConnectionEndpoint<TNodeId>, object?>(initialData);
        var executionNodes = nodes.ToDictionary(n => n.Id, n => new ExecutionNode<TNode, TNodeId>(n));
        var connectionList = connections.ToList();
        var outputProcessor = postProcessOutput ?? (val => val);

        // 1. 构建依赖图
        foreach (var connection in connectionList)
        {
            if (!executionNodes.TryGetValue(connection.Source.NodeId, out var sourceNode) ||
                !executionNodes.TryGetValue(connection.Target.NodeId, out var targetNode))
            {
                // 在理想模型中，这不应该发生，因为上层已经校验过了。
                continue;
            }

            targetNode.Dependencies.Add(sourceNode);
            sourceNode.Dependents.Add(targetNode);
        }

        // 2. 拓扑排序与并行执行
        var completedNodes = new ConcurrentDictionary<TNodeId, ExecutionNode<TNode, TNodeId>>();
        var readyToExecute =
            new ConcurrentQueue<ExecutionNode<TNode, TNodeId>>(executionNodes.Values.Where(n => n.Dependencies.Count == 0));

        while (completedNodes.Count < executionNodes.Count)
        {
            if (cancellationToken.IsCancellationRequested) return Result.Fail("执行已取消。");

            if (readyToExecute.IsEmpty)
            {
                var remainingNodeIds = string.Join(", ", executionNodes.Keys.Except(completedNodes.Keys));
                return Result.Fail($"图存在循环依赖或连接断裂，无法继续执行。剩余节点: {remainingNodeIds}");
            }

            var currentBatch = new List<ExecutionNode<TNode, TNodeId>>();
            while (readyToExecute.TryDequeue(out var node))
            {
                currentBatch.Add(node);
            }

            var tasks = currentBatch.Select(node =>
                    ExecuteSingleNodeInternalAsync(node, connectionList, dataStore, executeNodeAsync, outputProcessor, cancellationToken))
                .ToList();

            var results = await Task.WhenAll(tasks);

            // 3. 处理执行结果与更新依赖
            foreach (var result in results)
            {
                if (result.TryGetError(out var error, out var executedNode))
                {
                    return error; // 任何一个节点失败，整个图执行失败
                }

                completedNodes.TryAdd(executedNode.Node.Id, executedNode);
                foreach (var dependentNode in executedNode.Dependents)
                {
                    if (dependentNode.Dependencies.All(dep => completedNodes.ContainsKey(dep.Node.Id)))
                    {
                        readyToExecute.Enqueue(dependentNode);
                    }
                }
            }
        }

        return Result.Ok(dataStore);
    }

    // 内部辅助方法，执行单个节点
    private static async Task<Result<ExecutionNode<TNode, TNodeId>>> ExecuteSingleNodeInternalAsync<TNode, TNodeId>(
        ExecutionNode<TNode, TNodeId> node,
        IEnumerable<GraphConnection<TNodeId>> connections,
        ConcurrentDictionary<GraphConnectionEndpoint<TNodeId>, object?> dataStore,
        Func<TNode, IReadOnlyDictionary<string, object?>, CancellationToken, Task<Result<Dictionary<string, object?>>>> executeNodeAsync,
        Func<object?, object?> postProcessOutput,
        CancellationToken cancellationToken)
        where TNode : IGraphNode<TNodeId>
        where TNodeId : notnull
    {
        // 1. 准备输入
        var nodeInputs = new Dictionary<string, object?>();
        var connectionsToThisNode = connections.Where(c => c.Target.NodeId.Equals(node.Node.Id));
        foreach (var connection in connectionsToThisNode)
        {
            if (dataStore.TryGetValue(connection.Source, out var sourceValue))
            {
                nodeInputs[connection.Target.PortName] = sourceValue;
            }
            else
            {
                // 在理想模型中，这也不应发生。源数据应该总是存在。
                // 如果发生了，传递null，让节点执行逻辑自己决定如何处理。
                nodeInputs[connection.Target.PortName] = null;
            }
        }

        // 2. 执行节点
        var executeResult = await executeNodeAsync(node.Node, nodeInputs, cancellationToken);
        if (executeResult.TryGetError(out var error, out var nodeOutputs))
        {
            return Result.Fail("节点 '{node.Node.Id}' 执行失败。", error);
        }

        // 3. 存储输出
        foreach (var (portName, value) in nodeOutputs)
        {
            var outputEndpoint = new GraphConnectionEndpoint<TNodeId>(node.Node.Id, portName);
            dataStore[outputEndpoint] = postProcessOutput(value);
        }

        return Result.Ok(node);
    }

    // 内部辅助类，用于构建依赖图
    private class ExecutionNode<TNode, TNodeId>(TNode node)
        where TNode : IGraphNode<TNodeId>
        where TNodeId : notnull
    {
        public TNode Node { get; } = node;
        public ConcurrentBag<ExecutionNode<TNode, TNodeId>> Dependencies { get; } = [];
        public ConcurrentBag<ExecutionNode<TNode, TNodeId>> Dependents { get; } = [];
    }
}