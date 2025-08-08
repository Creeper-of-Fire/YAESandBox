using System.Reflection;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Rune;
using YAESandBox.Workflow.Tuum;

namespace YAESandBox.Workflow.Core.Analysis;

/// <summary>
/// 工作流的校验服务
/// </summary>
public class WorkflowValidationService
{
    /// <summary>
    /// 一个特殊的TuumId，用于表示工作流的入口。
    /// </summary>
    private const string WorkflowInputSourceId = "@workflow";

    /// <summary>
    /// 对整个工作流配置进行静态校验。
    /// </summary>
    public WorkflowValidationReport Validate(WorkflowConfig config)
    {
        var report = new WorkflowValidationReport();
        var allTuumConfigs = config.Tuums.ToDictionary(t => t.ConfigId);

        // 校验连接
        this.ValidateConnections(config, report, allTuumConfigs);

        // 校验数据流和输入完整性
        // 这个方法现在只检查Tuum节点的输入是否悬空
        this.ValidateDataFlow(config, report);

        // 检测循环依赖
        // 这个方法的错误将放入 GlobalMessages
        this.DetectCycles(config, report);

        return report;
    }

    /// <summary>
    /// 辅助方法，安全地向连接的校验结果中添加消息。
    /// </summary>
    private void AddMessageToConnection(WorkflowValidationReport report, WorkflowConnection connection, ValidationMessage message)
    {
        string connectionId = connection.GetId();
        if (!report.ConnectionMessages.TryGetValue(connectionId, out var messages))
        {
            messages = [];
            report.ConnectionMessages[connectionId] = messages;
        }

        messages.Add(message);
    }

    /// <summary>
    /// 校验所有连接的端点是否存在且合法。
    /// </summary>
    private void ValidateConnections(WorkflowConfig config, WorkflowValidationReport report,
        IReadOnlyDictionary<string, TuumConfig> allTuumConfigs)
    {
        foreach (var conn in config.Connections)
        {
            bool sourceTuumExists = conn.Source.TuumId == WorkflowInputSourceId || allTuumConfigs.ContainsKey(conn.Source.TuumId);
            bool targetTuumExists = allTuumConfigs.ContainsKey(conn.Target.TuumId);

            // 校验1: 源枢机和目标枢机是否存在
            if (!sourceTuumExists)
            {
                this.AddMessageToConnection(report, conn, new ValidationMessage
                {
                    Severity = RuleSeverity.Error,
                    Message = $"连接断开：找不到ID为 '{conn.Source.TuumId}' 的源枢机。",
                    RuleSource = "ConnectionValidation.MissingSourceTuum"
                });
            }

            if (!targetTuumExists)
            {
                this.AddMessageToConnection(report, conn, new ValidationMessage
                {
                    Severity = RuleSeverity.Error,
                    Message = $"连接断开：找不到ID为 '{conn.Target.TuumId}' 的目标枢机。",
                    RuleSource = "ConnectionValidation.MissingTargetTuum"
                });
            }

            // 如果源或目标枢机本身就不存在，后续的端点校验就没有意义了，直接跳过此连接的剩余检查。
            if (!sourceTuumExists || !targetTuumExists)
            {
                continue;
            }

            // 校验 2: 源端点是否存在
            if (conn.Source.TuumId == WorkflowInputSourceId)
            {
                // 情况 2a: 源是工作流的入口
                if (!config.WorkflowInputs.Contains(conn.Source.EndpointName))
                {
                    this.AddMessageToConnection(report, conn, new ValidationMessage
                    {
                        Severity = RuleSeverity.Error,
                        Message = $"连接错误：源端点 '{conn.Source.EndpointName}' 不是一个有效的工作流输入。",
                        RuleSource = "ConnectionValidation.InvalidWorkflowInput"
                    });
                }
            }
            else // 情况 2b: 源是一个普通的枢机
            {
                var sourceTuum = allTuumConfigs[conn.Source.TuumId];
                // 检查该枢机的所有输出映射的 "Value" 列表里，是否包含这个端点名
                if (!sourceTuum.OutputMappings.Values.SelectMany(v => v).Contains(conn.Source.EndpointName))
                {
                    this.AddMessageToConnection(report, conn, new ValidationMessage
                    {
                        Severity = RuleSeverity.Error,
                        Message = $"连接错误：源枢机 '{sourceTuum.Name}' (ID: {sourceTuum.ConfigId}) 没有一个名为 '{conn.Source.EndpointName}' 的输出端点。",
                        RuleSource = "ConnectionValidation.MissingSourceEndpoint"
                    });
                }
            }

            // 校验 3: 目标端点是否存在
            var targetTuum = allTuumConfigs[conn.Target.TuumId];
            // 检查该枢机的输入映射的 "Key" 里，是否包含这个端点名
            if (!targetTuum.InputMappings.ContainsKey(conn.Target.EndpointName))
            {
                this.AddMessageToConnection(report, conn, new ValidationMessage
                {
                    Severity = RuleSeverity.Error,
                    Message = $"连接错误：目标枢机 '{targetTuum.Name}' (ID: {targetTuum.ConfigId}) 没有一个名为 '{conn.Target.EndpointName}' 的输入端点。",
                    RuleSource = "ConnectionValidation.MissingTargetEndpoint"
                });
            }
        }
    }

    /// <summary>
    /// 校验数据流，确保所有输入都被连接，且没有重复连接。
    /// </summary>
    private void ValidateDataFlow(WorkflowConfig config, WorkflowValidationReport report)
    {
        var targetEndpoints = config.Connections.Select(c => c.Target).ToList();

        foreach (var tuum in config.Tuums)
        {
            var tuumResult = report.TuumResults.GetOrAdd(tuum.ConfigId, () => new TuumAnalysisResult());
            // 声明的输入端点现在是 InputMappings 的 Keys
            var declaredInputEndpoints = tuum.InputMappings.Keys;

            foreach (string inputEndpointName in declaredInputEndpoints)
            {
                int connectionsToThisInput = targetEndpoints.Count(t => t.TuumId == tuum.ConfigId && t.EndpointName == inputEndpointName);

                if (connectionsToThisInput == 0)
                {
                    tuumResult.Messages.Add(new ValidationMessage
                    {
                        Severity = RuleSeverity.Error,
                        Message = $"输入端点 '{inputEndpointName}' 未被连接。",
                        RuleSource = "DataFlow"
                    });
                }
                else if (connectionsToThisInput > 1)
                {
                    tuumResult.Messages.Add(new ValidationMessage
                    {
                        Severity = RuleSeverity.Error,
                        Message = $"输入端点 '{inputEndpointName}' 被连接了 {connectionsToThisInput} 次，只能连接一次。",
                        RuleSource = "DataFlow"
                    });
                }
            }
        }
    }

    /// <summary>
    /// 使用DFS检测工作流中的循环依赖。
    /// </summary>
    private void DetectCycles(WorkflowConfig config, WorkflowValidationReport report)
    {
        var graph = config.Connections
            .Where(c => c.Source.TuumId != WorkflowInputSourceId) // 排除工作流入口
            .GroupBy(c => c.Source.TuumId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Target.TuumId).Distinct().ToList());

        var visiting = new HashSet<string>();
        var visited = new HashSet<string>();

        foreach (string tuumId in config.Tuums.Select(t => t.ConfigId))
        {
            if (!visited.Contains(tuumId) && this.HasCycle(tuumId, graph, visiting, visited, report, []))
            {
                // 错误已在 HasCycle 方法内报告
            }
        }
    }

    private bool HasCycle(string currentNodeId, IReadOnlyDictionary<string, List<string>> graph,
        HashSet<string> visiting, HashSet<string> visited, WorkflowValidationReport report, List<string> path)
    {
        visiting.Add(currentNodeId);
        path.Add(currentNodeId);

        if (graph.TryGetValue(currentNodeId, out var neighbors))
        {
            foreach (string neighborId in neighbors)
            {
                if (visiting.Contains(neighborId))
                {
                    // 发现循环
                    string cyclePath = string.Join(" -> ", path) + $" -> {neighborId}";
                    report.TuumResults.GetOrAdd(currentNodeId, () => new TuumAnalysisResult()).Messages.Add(new ValidationMessage
                    {
                        Severity = RuleSeverity.Fatal,
                        Message = $"检测到循环依赖：{cyclePath}",
                        RuleSource = "CycleDetection"
                    });
                    return true;
                }

                if (!visited.Contains(neighborId))
                {
                    if (this.HasCycle(neighborId, graph, visiting, visited, report, path))
                    {
                        return true;
                    }
                }
            }
        }

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