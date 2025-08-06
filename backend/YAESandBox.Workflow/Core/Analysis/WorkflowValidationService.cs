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

        // 校验每个祝祷自身，并收集所有端点信息
        this.ValidateIndividualTuums(config, report);

        // 校验连接
        this.ValidateConnections(config, report, allTuumConfigs);

        // 校验数据流和输入完整性
        this.ValidateDataFlow(config, report);
        
        // 检测循环依赖
        this.DetectCycles(config, report);

        return report;
    }
    
    /// <summary>
    /// 遍历并校验每个祝祷的内部配置，包括输出映射和符文的Attribute规则。
    /// </summary>
    private void ValidateIndividualTuums(WorkflowConfig config, WorkflowValidationReport report)
    {
        foreach (var tuum in config.Tuums)
        {
            var tuumResult = report.TuumResults.GetOrAdd(tuum.ConfigId, () => new TuumValidationResult());

            // 校验1: 祝祷的输出映射是否引用了有效的内部变量
            this.ValidateTuumOutputMappingSource(tuum, tuumResult);
            
            // 新增校验 1.5: 校验输入和输出映射的唯一性规则
            this.ValidateTuumMappingUniqueness(tuum, tuumResult);

            // 校验 2: 祝祷内部的符文Attribute规则 (如 SingleInTuum, InFrontOf)
            this.ValidateInTuumRuneAttributeRules(tuum, tuumResult);
        }
    }

    /// <summary>
    /// 校验祝祷的OutputMappings是否引用了在内部真实存在的变量。
    /// (此方法需要微调以适应新结构)
    /// </summary>
    private void ValidateTuumOutputMappingSource(TuumConfig tuum, TuumValidationResult tuumResult)
    {
        // 祝祷内部，所有可用的变量名 = 外部注入的(InputMappings) + 内部产生的
        var allProducedInTuumVars = new HashSet<string>(tuum.Runes.SelectMany(r => r.GetProducedSpec().Select(p => p.Name)));
        var allInjectedInTuumVars = tuum.InputMappings.Values.SelectMany(v => v).ToHashSet();
        var allAvailableInTuumVars = allInjectedInTuumVars.Union(allProducedInTuumVars).ToHashSet();

        foreach ((string internalVarName, var endpointNames) in tuum.OutputMappings)
        {
            if (!allAvailableInTuumVars.Contains(internalVarName))
            {
                // 注意：我们将错误附加到祝祷上，因为这是关于整个祝祷映射配置的错误
                tuumResult.TuumMessages.Add(new ValidationMessage
                {
                    Severity = RuleSeverity.Error,
                    Message = $"输出映射错误：源内部变量 '{internalVarName}' 在此祝祷中从未被定义或产生。",
                    RuleSource = "DataFlow"
                });
            }
        }
    }

    /// <summary>
    /// 校验祝祷的输入和输出映射是否满足唯一性约束。
    /// </summary>
    private void ValidateTuumMappingUniqueness(TuumConfig tuum, TuumValidationResult tuumResult)
    {
        // 校验规则1：一个内部变量只能有一个数据源 (在所有InputMappings的Value列表中唯一)
        var allTargetInternalVars = tuum.InputMappings.Values.SelectMany(v => v);
        var duplicateInternalVars = allTargetInternalVars
            .GroupBy(v => v)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicateVar in duplicateInternalVars)
        {
            tuumResult.TuumMessages.Add(new ValidationMessage
            {
                Severity = RuleSeverity.Error,
                Message = $"输入映射冲突：内部变量 '{duplicateVar}' 被多个外部输入端点驱动，它只能有一个数据源。",
                RuleSource = "MappingUniqueness"
            });
        }
        
        // 校验规则2：一个外部输出端点只能被一个内部变量驱动 (在所有OutputMappings的Value列表中唯一)
        var allTargetExternalEndpoints = tuum.OutputMappings.Values.SelectMany(v => v);
        var duplicateExternalEndpoints = allTargetExternalEndpoints
            .GroupBy(e => e)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var duplicateEndpoint in duplicateExternalEndpoints)
        {
            tuumResult.TuumMessages.Add(new ValidationMessage
            {
                Severity = RuleSeverity.Error,
                Message = $"输出映射冲突：外部端点 '{duplicateEndpoint}' 被多个内部变量驱动，它只能有一个数据源。",
                RuleSource = "MappingUniqueness"
            });
        }
    }
    
    /// <summary>
    /// 校验祝祷内部的符文是否满足其Attribute定义的规则。
    /// </summary>
    private void ValidateInTuumRuneAttributeRules(TuumConfig tuum, TuumValidationResult tuumResult)
    {
        for (int i = 0; i < tuum.Runes.Count; i++)
        {
            var rune = tuum.Runes[i];
            var runeType = rune.GetType();
            
            // 规则: [SingleInTuum]
            if (runeType.GetCustomAttribute<SingleInTuumAttribute>() != null)
            {
                if (tuum.Runes.Count(r => r.GetType() == runeType) > 1)
                {
                    this.AddMessageToRune(tuumResult, rune.ConfigId, new ValidationMessage
                    {
                        Severity = RuleSeverity.Warning,
                        Message = $"符文类型 '{runeType.Name}' 在此祝祷中出现了多次，但它被建议只使用一次。",
                        RuleSource = "SingleInTuum"
                    });
                }
            }
            
            // 规则: [InFrontOf] 和 [Behind]
            this.ValidateRelativeOrderInTuum(rune, tuum.Runes, i, tuumResult);
        }
    }
    
    /// <summary>
    /// 校验单个符文在其祝祷内的相对顺序。
    /// </summary>
    private void ValidateRelativeOrderInTuum(AbstractRuneConfig rune, List<AbstractRuneConfig> tuumRunes, int runeIndex, TuumValidationResult tuumResult)
    {
        var runeType = rune.GetType();

        // 规则: [InFrontOf]
        if (runeType.GetCustomAttribute<InFrontOfAttribute>() is { } inFrontOfAttr)
        {
            foreach (var targetType in inFrontOfAttr.InFrontOfType)
            {
                int targetIndex = tuumRunes.FindIndex(m => m.GetType() == targetType);
                if (targetIndex != -1 && runeIndex > targetIndex)
                {
                    this.AddMessageToRune(tuumResult, rune.ConfigId, new ValidationMessage
                    {
                        Severity = RuleSeverity.Warning,
                        Message = $"顺序警告：符文 '{runeType.Name}' 应该在 '{targetType.Name}' 之前执行。",
                        RuleSource = "InFrontOf"
                    });
                }
            }
        }

        // 规则: [Behind]
        if (runeType.GetCustomAttribute<BehindAttribute>() is { } behindAttr)
        {
            foreach (var targetType in behindAttr.BehindType)
            {
                int targetIndex = tuumRunes.FindIndex(m => m.GetType() == targetType);
                if (targetIndex != -1 && runeIndex < targetIndex)
                {
                    this.AddMessageToRune(tuumResult, rune.ConfigId, new ValidationMessage
                    {
                        Severity = RuleSeverity.Warning,
                        Message = $"顺序警告：符文 '{runeType.Name}' 应该在 '{targetType.Name}' 之后执行。",
                        RuleSource = "Behind"
                    });
                }
            }
        }
    }

    
    /// <summary>
    /// 校验所有连接的端点是否存在且合法。
    /// </summary>
    private void ValidateConnections(WorkflowConfig config, WorkflowValidationReport report, IReadOnlyDictionary<string, TuumConfig> allTuumConfigs)
    {
        foreach (var conn in config.Connections)
        {
            // 校验源端点
            if (conn.Source.TuumId == WorkflowInputSourceId)
            {
                if (!config.WorkflowInputs.Contains(conn.Source.EndpointName))
                {
                    report.TuumResults.GetOrAdd(conn.Target.TuumId, () => new TuumValidationResult()).TuumMessages.Add(new ValidationMessage
                    {
                        Severity = RuleSeverity.Error,
                        Message = $"连接错误：源端点 '{conn.Source.EndpointName}' 不是一个有效的工作流输入。",
                        RuleSource = "ConnectionValidation"
                    });
                }
            }
            else if (allTuumConfigs.TryGetValue(conn.Source.TuumId, out var sourceTuum))
            {
                // 一个内部变量可以驱动多个外部端点，所以我们需要检查所有列表
                if (!sourceTuum.OutputMappings.Values.SelectMany(v => v).Contains(conn.Source.EndpointName))
                {
                    report.TuumResults.GetOrAdd(sourceTuum.ConfigId, () => new TuumValidationResult()).TuumMessages.Add(new ValidationMessage
                    {
                        Severity = RuleSeverity.Error,
                        Message = $"连接错误：此祝祷没有一个名为 '{conn.Source.EndpointName}' 的输出端点。",
                        RuleSource = "ConnectionValidation"
                    });
                }
            }
            else
            {
                // 如果源祝祷ID本身就找不到，将错误信息附加到目标祝祷上，因为这是连接的另一端。
                if (allTuumConfigs.ContainsKey(conn.Target.TuumId))
                {
                    report.TuumResults.GetOrAdd(conn.Target.TuumId, () => new TuumValidationResult()).TuumMessages.Add(new ValidationMessage
                    {
                        Severity = RuleSeverity.Error,
                        Message = $"连接错误：找不到ID为 '{conn.Source.TuumId}' 的源祝祷。",
                        RuleSource = "ConnectionValidation"
                    });
                }
            }

            // 校验目标端点
            if (allTuumConfigs.TryGetValue(conn.Target.TuumId, out var targetTuum))
            {
                // 检查Key中是否存在该输入端点
                if (!targetTuum.InputMappings.ContainsKey(conn.Target.EndpointName))
                {
                    report.TuumResults.GetOrAdd(targetTuum.ConfigId, () => new TuumValidationResult()).TuumMessages.Add(new ValidationMessage
                    {
                        Severity = RuleSeverity.Error,
                        Message = $"连接错误：此祝祷没有一个名为 '{conn.Target.EndpointName}' 的输入端点。",
                        RuleSource = "ConnectionValidation"
                    });
                }
            }
            else
            {
                 // 如果目标祝祷ID本身就找不到，将错误附加到源祝祷上。
                 if(allTuumConfigs.ContainsKey(conn.Source.TuumId))
                 {
                    report.TuumResults.GetOrAdd(conn.Source.TuumId, () => new TuumValidationResult()).TuumMessages.Add(new ValidationMessage
                    {
                        Severity = RuleSeverity.Error,
                        Message = $"连接错误：找不到ID为 '{conn.Target.TuumId}' 的目标祝祷。",
                        RuleSource = "ConnectionValidation"
                    });
                 }
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
            var tuumResult = report.TuumResults.GetOrAdd(tuum.ConfigId, () => new TuumValidationResult());
            // 声明的输入端点现在是 InputMappings 的 Keys
            var declaredInputEndpoints = tuum.InputMappings.Keys;

            foreach (var inputEndpointName in declaredInputEndpoints)
            {
                var connectionsToThisInput = targetEndpoints.Count(t => t.TuumId == tuum.ConfigId && t.EndpointName == inputEndpointName);

                if (connectionsToThisInput == 0)
                {
                    tuumResult.TuumMessages.Add(new ValidationMessage
                    {
                        Severity = RuleSeverity.Error,
                        Message = $"输入端点 '{inputEndpointName}' 未被连接。",
                        RuleSource = "DataFlow"
                    });
                }
                else if (connectionsToThisInput > 1)
                {
                    tuumResult.TuumMessages.Add(new ValidationMessage
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

        foreach (var tuumId in config.Tuums.Select(t => t.ConfigId))
        {
            if (!visited.Contains(tuumId) && HasCycle(tuumId, graph, visiting, visited, report, []))
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
            foreach (var neighborId in neighbors)
            {
                if (visiting.Contains(neighborId))
                {
                    // 发现循环
                    var cyclePath = string.Join(" -> ", path) + $" -> {neighborId}";
                    report.TuumResults.GetOrAdd(currentNodeId, () => new TuumValidationResult()).TuumMessages.Add(new ValidationMessage
                    {
                        Severity = RuleSeverity.Fatal,
                        Message = $"检测到循环依赖：{cyclePath}",
                        RuleSource = "CycleDetection"
                    });
                    return true;
                }

                if (!visited.Contains(neighborId))
                {
                    if (HasCycle(neighborId, graph, visiting, visited, report, path))
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

    /// <summary>
    /// 辅助方法，安全地向符文的校验结果中添加消息。
    /// </summary>
    private void AddMessageToRune(TuumValidationResult tuumResult, string runeId, ValidationMessage message)
    {
        if (!tuumResult.RuneResults.TryGetValue(runeId, out var runeResult))
        {
            runeResult = new RuneValidationResult();
            tuumResult.RuneResults[runeId] = runeResult;
        }
        
        // 防止对同一个符文添加完全相同的警告信息
        if (!runeResult.RuneMessages.Any(m => m.RuleSource == message.RuleSource && m.Message == message.Message))
        {
            runeResult.RuneMessages.Add(message);
        }
    }
}

/// <summary>
/// 字典的扩展方法
/// </summary>
internal static class DictionaryExtensions
{
    public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, Func<TValue> valueFactory) where TKey : notnull
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