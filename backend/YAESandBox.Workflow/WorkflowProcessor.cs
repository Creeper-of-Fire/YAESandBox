using YAESandBox.Depend.Results;
using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Tuum;
using YAESandBox.Workflow.Utility;

namespace YAESandBox.Workflow;

internal class WorkflowProcessor(
    WorkflowRuntimeService runtimeService,
    WorkflowProcessorConfig config,
    Dictionary<string, string> triggerParams)
    : IWithDebugDto<IWorkflowProcessorDebugDto>
{
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

    private List<TuumProcessor> Tuums { get; } = config.Tuums.ConvertAll(it => it.ToTuumProcessor(runtimeService));

    private WorkflowRuntimeService RuntimeService { get; } = runtimeService;
    private WorkflowRuntimeContext Context { get; } = new(triggerParams);


    /// <summary>
    /// 封装工作流执行期间的有状态数据。
    /// 这个对象是可变的，并在整个工作流的祝祷之间传递和修改。
    /// </summary>
    public class WorkflowRuntimeContext(IReadOnlyDictionary<string, string> triggerParams)
    {
        // /// <summary>
        // /// 工作流的输入参数被放入这里，
        // /// </summary>
        // public IReadOnlyDictionary<string, string> TriggerParams { get; } = triggerParams;

        /// <summary>
        /// 全局变量池。
        /// 每个祝祷的输出可以写回这里，供后续祝祷使用。
        /// </summary>
        public Dictionary<string, object> GlobalVariables { get; } = triggerParams.ToDictionary(kv => kv.Key, object (kv) => kv.Value);

        // /// <summary>
        // /// 最终生成的、要呈现给用户的原始文本。
        // /// </summary>
        // public string FinalRawText =>
        //     this.GlobalVariables.GetValueOrDefault(nameof(this.FinalRawText)) as string ?? string.Empty;

        // /// <summary>
        // /// 整个工作流生成的所有原子操作列表。
        // /// </summary>
        // public List<IWorkflowAtomicOperation> GeneratedOperations =>
        //     this.GlobalVariables.GetValueOrDefault(nameof(this.GeneratedOperations)) as List<IWorkflowAtomicOperation> ?? [];
    }


    public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(CancellationToken cancellationToken = default)
    {
        // --- 1. 依赖分析与执行计划生成 ---

        // 节点列表，每个节点代表一个祝祷
        var nodes = this.Tuums.ToDictionary(tuum => tuum, tuum => new ExecutionNode(tuum));

        // 构建依赖图
        var tuumWithIndex = this.Tuums.Select((tuum, index) => new { tuum, index }).ToList();

        foreach (var current in tuumWithIndex)
        {
            var currentNode = nodes[current.tuum];

            // 遍历当前祝祷消费的所有变量
            foreach (string consumedVar in current.tuum.GlobalConsumers)
            {
                // 在当前祝祷【之前】的所有祝祷中，寻找该变量的生产者
                for (int i = 0; i < current.index; i++)
                {
                    var potentialProducer = tuumWithIndex[i];
                    if (potentialProducer.tuum.GlobalProducers.Contains(consumedVar))
                    {
                        // 找到了一个前置生产者，添加依赖关系
                        var producerNode = nodes[potentialProducer.tuum];
                        currentNode.Dependencies.Add(producerNode);
                        producerNode.Dependents.Add(currentNode);
                    }
                }
            }
        }


        // --- 2. 拓扑排序与并行执行 ---

        // 使用一个 Set 来跟踪已完成的节点，防止重复执行
        var completedNodes = new HashSet<ExecutionNode>();

        // 初始时，所有没有依赖的节点都可以作为第一批并行任务
        var readyToExecute = new Queue<ExecutionNode>(nodes.Values.Where(n => n.Dependencies.Count == 0));

        // 当还有节点在队列中或在执行时，循环继续
        while (nodes.Count > completedNodes.Count)
        {
            // 如果没有可执行的任务，但还有未完成的节点，说明有循环依赖
            // 注意：在当前基于列表顺序的设计中，这种情况理论上不应发生，
            // 除非配置文件被手动修改或存在bug。
            // 保留此检测是为了系统的健壮性和未来的扩展性。
            if (readyToExecute.Count == 0)
            {
                string remainingNodeNames = string.Join(", ", nodes.Values.Except(completedNodes).Select(n => n.Tuum.Config.ConfigId));
                return new WorkflowExecutionResult(false, $"工作流存在循环依赖，无法继续执行。剩余节点: {remainingNodeNames}", "CircularDependency");
            }

            // 从队列中取出当前批次所有可执行的任务
            var currentBatch = new List<ExecutionNode>();
            while (readyToExecute.TryDequeue(out var node))
            {
                currentBatch.Add(node);
            }

            // 并行执行当前批次的所有祝祷
            var tasks = currentBatch.Select(node => this.ExecuteSingleTuumAsync(node.Tuum, cancellationToken)).ToList();
            var results = await Task.WhenAll(tasks);

            // --- 3. 处理执行结果与更新依赖 ---

            foreach (var result in results)
            {
                if (result.TryGetError(out var error))
                {
                    // 任何一个并行祝祷失败，则整个工作流失败
                    return new WorkflowExecutionResult(false, error.Message, "TuumExecutionFailed");
                }
            }

            // 将本批次成功完成的节点标记为已完成
            foreach (var executedNode in currentBatch)
            {
                completedNodes.Add(executedNode);

                // 遍历每个已完成节点的“后继者”（依赖它的节点）
                foreach (var dependentNode in executedNode.Dependents)
                {
                    // 检查这个后继者的所有依赖是否都已完成
                    bool allDependenciesMet = dependentNode.Dependencies.All(dep => completedNodes.Contains(dep));
                    if (allDependenciesMet)
                    {
                        // 如果所有依赖都满足了，将这个后继者加入到下一批执行队列
                        readyToExecute.Enqueue(dependentNode);
                    }
                }
            }
        }

        // 所有节点都成功执行完毕后，构造一个成功的最终结果
        return new WorkflowExecutionResult(true, null, null);
    }

    /// <summary>
    /// 辅助方法：执行单个祝祷并将其结果合并到全局上下文
    /// </summary>
    private async Task<Result> ExecuteSingleTuumAsync(TuumProcessor tuum, CancellationToken cancellationToken)
    {
        var tuumResult = await tuum.ExecuteTuumsAsync(this.Context, cancellationToken);
        if (tuumResult.TryGetError(out var error, out var tuumOutput))
        {
            return error;
        }

        // 使用锁来保证并行写入全局变量池的线程安全
        lock (this.Context.GlobalVariables)
        {
            foreach (var outputVariable in tuumOutput)
            {
                this.Context.GlobalVariables[outputVariable.Key] = outputVariable.Value;
            }
        }

        return Result.Ok();
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