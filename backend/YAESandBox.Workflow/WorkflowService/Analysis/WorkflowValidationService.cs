using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using YAESandBox.Depend.Results;
using YAESandBox.Workflow.Core.Config;
using YAESandBox.Workflow.Core.Config.RuneConfig;
using YAESandBox.Workflow.Core.Graph;
using YAESandBox.Workflow.Core.Runtime.WorkflowService;
using YAESandBox.Workflow.Core.VarSpec;

namespace YAESandBox.Workflow.WorkflowService.Analysis;

/// <summary>
/// 整个工作流的校验报告。
/// </summary>
public record WorkflowValidationReport
{
    /// <summary>
    /// 每个枢机的校验结果。
    /// Key是枢机的ConfigId。
    /// </summary>
    [Required]
    public Dictionary<string, TuumAnalysisResult> TuumResults { get; init; } = [];

    /// <summary>
    /// Key: Connection的唯一标识符
    /// </summary>
    [Required]
    public Dictionary<string, List<ValidationMessage>> ConnectionMessages { get; init; } = [];

    /// <summary>
    /// 对整个工作流所有可观察副作用（发射的事件）的静态描述。
    /// 这构成了工作流对外的“事件API”文档。
    /// </summary>
    [Required]
    public List<EmittedEventSpec> WorkflowEmittedEvents { get; init; } = [];

    /// <summary>
    /// 用于存放循环依赖等无法归属到任何单一实体的错误
    /// </summary>
    [Required]
    public List<ValidationMessage> GlobalMessages { get; init; } = [];
}

/// <summary>
/// 工作流的校验服务
/// </summary>
public class WorkflowValidationService(TuumAnalysisService tuumAnalysisService)
{
    private TuumAnalysisService TuumAnalysisService { get; } = tuumAnalysisService;

    /// <summary>
    /// 对整个工作流配置进行静态校验。
    /// </summary>
    public WorkflowValidationReport Validate(WorkflowConfig config, TypeAnalysisMode mode = TypeAnalysisMode.DuckTyping)
    {
        var report = new WorkflowValidationReport();

        // 步骤 1: 对每个Tuum进行独立的深度分析
        foreach (var tuumConfig in config.Tuums.Where(t => t.Enabled))
        {
            var tuumResult = this.TuumAnalysisService.Analyze(tuumConfig, mode);
            report.TuumResults[tuumConfig.ConfigId] = tuumResult;
        }

        // 步骤 2: 确定工作流的“有效连接图”
        var (connectionMessages, effectiveConnections) = DetermineEffectiveConnections(config, report);
        foreach ((string connId, var messages) in connectionMessages)
        {
            report.ConnectionMessages.GetOrAdd(connId, () => []).AddRange(messages);
        }

        // 步骤 3: 基于“有效连接图”进行全局校验
        this.ValidateEffectiveConnections(report, effectiveConnections, mode);
        this.ValidateDataFlow(config, report, effectiveConnections);
        this.DetectCycles(config, report, effectiveConnections);

        // 步骤 4: 聚合整个工作流的副作用，生成最终的“API文档”
        report.WorkflowEmittedEvents.AddRange(
            report.TuumResults.Values.SelectMany(tr => tr.EmittedEvents)
        );

        return report;
    }

    /// <summary>
    /// 根据工作流配置（自动或手动），确定最终生效的连接列表。
    /// </summary>
    private static (Dictionary<string, List<ValidationMessage>> messages, List<GraphConnection<string>> connections)
        DetermineEffectiveConnections(WorkflowConfig config, WorkflowValidationReport report)
    {
        // 默认或显式启用自动连接
        if (config.Graph?.EnableAutoConnect != false)
        {
            var virtualInputNode = new VirtualWorkflowInputNode(config, new Dictionary<string, object?>());
            var tuumGraphNodes = config.Tuums
                .Where(t => t.Enabled)
                .Select(t => new TuumGraphNode(t))
                .ToList<IGraphNode<string>>();

            var allNodes = new List<IGraphNode<string>> { virtualInputNode };
            allNodes.AddRange(tuumGraphNodes);

            // GraphExecutor.TryAutoConnect 返回 Result，我们需要将其转换为 ValidationMessage
            var autoConnectResult = GraphExecutor.TryAutoConnect<IGraphNode<string>, string>(allNodes);
            if (autoConnectResult.TryGetError(out var error, out var autoConnections))
            {
                // 自动连接失败是一个全局性错误
                report.GlobalMessages.Add(new ValidationMessage
                {
                    Severity = RuleSeverity.Error,
                    Message = $"自动连接失败：{error.ToDetailString()}",
                    RuleSource = "Graph.AutoConnection"
                });
                return (new Dictionary<string, List<ValidationMessage>>(), []);
            }

            return (new Dictionary<string, List<ValidationMessage>>(), autoConnections);
        }

        // 使用手动连接
        var manualConnections = config.Graph.Connections ?? [];
        return (new Dictionary<string, List<ValidationMessage>>(), manualConnections.Select(c =>
            new GraphConnection<string>(
                new GraphConnectionEndpoint<string>(c.Source.TuumId, c.Source.EndpointName),
                new GraphConnectionEndpoint<string>(c.Target.TuumId, c.Target.EndpointName)
            )).ToList());
    }

    /// <summary>
    /// 校验所有生效连接的端点存在性和类型兼容性。
    /// </summary>
    private void ValidateEffectiveConnections(WorkflowValidationReport report, List<GraphConnection<string>> connections,
        TypeAnalysisMode mode)
    {
        foreach (var conn in connections)
        {
            string connId = conn.GetId();

            // --- 获取源和目标的类型定义 ---
            if (!this.TryGetEndpointDef(report, conn.Source, out var sourceDef, out var sourceMessage))
            {
                report.ConnectionMessages.GetOrAdd(connId, () => []).Add(sourceMessage);
                continue; // 源不存在，无法继续校验
            }


            if (!this.TryGetEndpointDef(report, conn.Target, out var targetDef, out var targetMessage, isSource: false))
            {
                report.ConnectionMessages.GetOrAdd(connId, () => []).Add(targetMessage);
                continue; // 目标不存在，无法继续校验
            }

            // --- 类型兼容性校验 ---
            var compatibilityResult = sourceDef.CheckCompatibilityWith(targetDef, mode);
            if (!compatibilityResult.IsSuccess)
            {
                report.ConnectionMessages.GetOrAdd(connId, () => []).Add(new ValidationMessage
                {
                    Severity = RuleSeverity.Error,
                    Message = $"类型不兼容：从 '{conn.Source.NodeId}' 的端口 '{conn.Source.PortName}' ({sourceDef.TypeName}) " +
                              $"到 '{conn.Target.NodeId}' 的端口 '{conn.Target.PortName}' ({targetDef.TypeName})。原因: {compatibilityResult.FailureReason}",
                    RuleSource = "Connection.TypeCompatibility"
                });
            }
        }
    }

    /// <summary>
    /// 辅助方法，尝试从分析报告中查找指定端点的类型定义。
    /// </summary>
    /// <returns>如果找到端点，则返回 true，def 不为 null，message 为 null。否则返回 false。</returns>
    private bool TryGetEndpointDef(
        WorkflowValidationReport report,
        GraphConnectionEndpoint<string> endpoint,
        [MaybeNullWhen(false)] out VarSpecDef def,
        [MaybeNullWhen(true)] out ValidationMessage message,
        bool isSource = true)
    {
        if (isSource && endpoint.NodeId == VirtualWorkflowInputNode.VIRTUAL_INPUT_ID)
        {
            def = CoreVarDefs.Any;
            message = null;
            return true;
        }

        if (!report.TuumResults.TryGetValue(endpoint.NodeId, out var tuumResult))
        {
            def = null;
            message = new ValidationMessage
                { Severity = RuleSeverity.Error, Message = $"连接错误：找不到ID为 '{endpoint.NodeId}' 的枢机。", RuleSource = "Connection.MissingTuum" };
            return false;
        }

        def = isSource
            ? tuumResult.ProducedEndpoints.FirstOrDefault(p => p.Name == endpoint.PortName)?.Def
            : tuumResult.ConsumedEndpoints.FirstOrDefault(c => c.Name == endpoint.PortName)?.Def;

        if (def is null)
        {
            string direction = isSource ? "输出" : "输入";
            message = new ValidationMessage
            {
                Severity = RuleSeverity.Error, Message = $"连接错误：在枢机 '{endpoint.NodeId}' 上找不到名为 '{endpoint.PortName}' 的{direction}端点。",
                RuleSource = "Connection.MissingEndpoint"
            };
            return false;
        }

        message = null;
        return true;
    }

    private void ValidateDataFlow(WorkflowConfig config, WorkflowValidationReport report, List<GraphConnection<string>> connections)
    {
        var targetsLookup = connections.ToLookup(c => c.Target);

        foreach (var tuum in config.Tuums.Where(t => t.Enabled))
        {
            var tuumResult = report.TuumResults[tuum.ConfigId];
            foreach (var requiredInput in tuumResult.ConsumedEndpoints.Where(c => !c.IsOptional))
            {
                var connectionsToInput = targetsLookup[new GraphConnectionEndpoint<string>(tuum.ConfigId, requiredInput.Name)].ToList();

                if (connectionsToInput.Count == 0)
                {
                    tuumResult.Messages.Add(new ValidationMessage
                    {
                        Severity = RuleSeverity.Error,
                        Message = $"必需的输入端点 '{requiredInput.Name}' 未被连接。",
                        RuleSource = "DataFlow.MissingConnection"
                    });
                }
                else if (connectionsToInput.Count > 1)
                {
                    tuumResult.Messages.Add(new ValidationMessage
                    {
                        Severity = RuleSeverity.Error,
                        Message = $"输入端点 '{requiredInput.Name}' 被连接了 {connectionsToInput.Count} 次，但它只能有一个数据源。",
                        RuleSource = "DataFlow.MultipleConnections"
                    });
                }
            }
        }
    }

    /// <summary>
    /// 使用DFS检测工作流中的循环依赖。
    /// </summary>
    private void DetectCycles(WorkflowConfig config, WorkflowValidationReport report, List<GraphConnection<string>> connections)
    {
        // 1. 构建邻接表形式的图
        var graph = connections
            // 排除来自虚拟工作流入口的连接，因为它不参与循环
            .Where(c => c.Source.NodeId != VirtualWorkflowInputNode.VIRTUAL_INPUT_ID)
            .GroupBy(c => c.Source.NodeId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Target.NodeId).Distinct().ToList());

        var visiting = new HashSet<string>(); // 存储当前DFS路径上的节点
        var visited = new HashSet<string>(); // 存储已经完整访问过的节点（及其所有子节点）

        // 遍历所有Tuum节点，以确保能检测到图中所有可能的循环（即使图不是完全连通的）
        foreach (string tuumId in config.Tuums.Where(t => t.Enabled).Select(t => t.ConfigId))
        {
            if (!visited.Contains(tuumId))
            {
                // HasCycle 方法会在发现循环时直接将错误信息添加到报告中
                HasCycle(tuumId, graph, visiting, visited, report, []);
            }
        }
    }

    /// <summary>
    /// DFS的递归辅助方法，用于检测循环。
    /// </summary>
    /// <returns>如果从当前节点出发发现了循环，则返回 true。</returns>
    private static bool HasCycle(
        string currentNodeId,
        IReadOnlyDictionary<string, List<string>> graph,
        HashSet<string> visiting,
        HashSet<string> visited,
        WorkflowValidationReport report,
        List<string> path)
    {
        visiting.Add(currentNodeId);
        path.Add(currentNodeId);

        if (graph.TryGetValue(currentNodeId, out var neighbors))
        {
            foreach (string neighborId in neighbors)
            {
                // 如果邻居节点正在当前的访问路径上，说明发现了回边，即存在循环
                if (visiting.Contains(neighborId))
                {
                    // 发现循环！
                    string cyclePath = string.Join(" -> ", path) + $" -> {neighborId}";
                    report.GlobalMessages.Add(new ValidationMessage
                    {
                        Severity = RuleSeverity.Error,
                        Message = $"检测到循环依赖：{cyclePath}",
                        RuleSource = "Graph.CycleDetection"
                    });
                    return true;
                }

                // 如果邻居节点尚未被完整访问过，则继续深入递归
                if (!visited.Contains(neighborId))
                {
                    if (HasCycle(neighborId, graph, visiting, visited, report, path))
                    {
                        // 如果下游的递归调用发现了循环，则立即向上返回 true
                        return true;
                    }
                }
            }
        }

        // 当前节点的所有邻居都已访问完毕，没有发现循环
        // 将其从“正在访问”集合中移除，并加入“已访问”集合
        visiting.Remove(currentNodeId);
        path.RemoveAt(path.Count - 1);
        visited.Add(currentNodeId);

        return false;
    }
}

/// <summary>
/// 字典的扩展方法
/// </summary>
internal static class DictionaryExtensions
{
    public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, Func<TValue> valueFactory)
        where TKey : notnull
    {
        if (dictionary.TryGetValue(key, out var value))
        {
            return value;
        }

        var newValue = valueFactory();
        dictionary[key] = newValue;
        return newValue;
    }
}