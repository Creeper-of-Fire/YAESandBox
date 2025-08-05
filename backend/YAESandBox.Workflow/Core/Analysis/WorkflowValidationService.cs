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
    /// 校验
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    public WorkflowValidationReport Validate(WorkflowConfig config)
    {
        var report = new WorkflowValidationReport();

        // 初始时，可用的变量池只包含工作流声明的触发参数
        var availableVariables = new HashSet<string>(config.TriggerParams);

        var tuumRunes = config.Tuums
            .Select(s => new { Tuum = s, Runes = s.Runes })
            .ToList();

        for (int i = 0; i < tuumRunes.Count; i++)
        {
            var currentTuumInfo = tuumRunes[i];
            var tuum = currentTuumInfo.Tuum;
            var tuumResult = new TuumValidationResult();

            // 1. 进行数据流校验 (变量的生产与消费)
            this.ValidateDataFlow(tuum, availableVariables, tuumResult);

            // 2. 进行基于Attribute的拓扑/结构规则校验
            this.ValidateAttributeRules(tuum, config.Tuums, i, tuumResult);

            // 如果当前祝祷有任何校验信息，就添加到报告中
            if (tuumResult.RuneResults.Any() || tuumResult.TuumMessages.Any())
            {
                report.TuumResults[tuum.ConfigId] = tuumResult;
            }

            // 3. 在处理完一个祝祷后，将其产生的全局变量加入可用池，供后续祝祷使用
            foreach (string producedVar in tuum.OutputMappings.Keys)
            {
                availableVariables.Add(producedVar);
            }
        }

        return report;
    }

    /// <summary>
    /// 校验数据流：检查所有符文消费的变量是否都已在之前的祝祷、触发参数或本祝祷的前置符文中定义。
    /// </summary>
    private void ValidateDataFlow(TuumConfig tuum, ISet<string> initialAvailableVariables, TuumValidationResult tuumResult)
    {
        // 1. (保持不变) 检查 InputMappings 的源（Value，即全局变量）是否存在于上游的可用池中
        foreach (string globalName in tuum.InputMappings.Values.ToHashSet())
        {
            if (!initialAvailableVariables.Contains(globalName))
            {
                tuumResult.TuumMessages.Add(new ValidationMessage
                {
                    Severity = RuleSeverity.Error,
                    Message = $"输入映射错误：上游未提供可用的全局变量 '{globalName}'。",
                    RuleSource = "DataFlow"
                });
            }
        }

        // 2. 模拟祝祷内部的执行流程，动态计算每个符文执行前的可用变量池
        //    这个变量池包含了“外部注入的”和“内部生产的”所有变量。
        var inTuumAvailableVars = new HashSet<string>(tuum.InputMappings.Keys);

        foreach (var rune in tuum.Runes)
        {
            // 2a. 对当前符文进行校验：检查其消费的变量是否都存在于【当前】的可用变量池中
            var requiredVars = rune.GetConsumedVariables();
            var missingVars = requiredVars.Except(inTuumAvailableVars);

            foreach (string missingVar in missingVars)
            {
                this.AddMessageToRune(tuumResult, rune.ConfigId, new ValidationMessage
                {
                    Severity = RuleSeverity.Error,
                    Message = $"符文必需的输入变量 '{missingVar}' 未被提供。它既没有在祝祷的 InputMappings 中映射，也不是由本祝祷中任何前置符文产生的。",
                    RuleSource = "DataFlow"
                });
            }

            // 2b. 更新可用变量池：将当前符文【生产】的变量添加到池中，供后续符文使用
            var producedVars = rune.GetProducedVariables();
            foreach (string producedVar in producedVars)
            {
                inTuumAvailableVars.Add(producedVar);
            }
        }

        // 3. (新增) 检查 OutputMappings 的源（Value，即祝祷内部变量）是否真的被生产出来了
        var allProducedInTuumVars = new HashSet<string>(tuum.Runes.SelectMany(m => m.GetProducedVariables()));
        var allAvailableInTuumVars = new HashSet<string>(tuum.InputMappings.Keys).Union(allProducedInTuumVars).ToHashSet();

        foreach ((string globalTargetVar, string localSourceVar) in tuum.OutputMappings)
        {
            if (allAvailableInTuumVars.Contains(localSourceVar))
                continue;

            tuumResult.TuumMessages.Add(new ValidationMessage
            {
                Severity = RuleSeverity.Error,
                Message = $"输出映射错误：尝试将不存在的内部变量 '{localSourceVar}' 映射到全局变量 '{globalTargetVar}'。",
                RuleSource = "DataFlow"
            });
        }
    }

    /// <summary>
    /// 校验基于Attribute的规则，如[SingleInTuum], [InLastTuum]等。
    /// </summary>
    private void ValidateAttributeRules(TuumConfig tuum, List<TuumConfig> allTuums, int currentIndex,
        TuumValidationResult tuumResult)
    {
        // a. [InLastTuum] 校验
        var finalRune = tuum.Runes.FirstOrDefault(m => m.GetType().GetCustomAttribute<InLastTuumAttribute>() != null);
        if (finalRune != null && currentIndex != allTuums.Count - 1)
        {
            // 这是一个祝祷级别的错误
            tuumResult.TuumMessages.Add(new ValidationMessage
            {
                Severity = RuleSeverity.Error,
                Message = $"此祝祷包含一个终结符文 ({finalRune.GetType().Name})，但它不是工作流的最后一个祝祷。",
                RuleSource = "InLastTuum"
            });
        }

        // 遍历符文进行符文级Attribute校验
        foreach (var rune in tuum.Runes)
        {
            var runeType = rune.GetType();

            // b. [SingleInTuum] 校验
            if (runeType.GetCustomAttribute<SingleInTuumAttribute>() != null)
            {
                if (tuum.Runes.Count(m => m.GetType() == runeType) > 1)
                {
                    this.AddMessageToRune(tuumResult, rune.ConfigId, new ValidationMessage
                    {
                        // 这通常是一个警告，因为逻辑上可能允许（按顺序执行），但不是最佳实践
                        Severity = RuleSeverity.Warning,
                        Message = $"符文类型 '{runeType.Name}' 在此祝祷中出现了多次，但它被建议只使用一次。",
                        RuleSource = "SingleInTuum"
                    });
                }
            }

            // c. [InFrontOf] / [Behind] 校验 (祝祷内部顺序)
            this.ValidateRelativeOrder(rune, tuum.Runes, tuumResult);
        }
    }

    /// <summary>
    /// 校验单个符文在其祝祷内的相对顺序。
    /// </summary>
    private void ValidateRelativeOrder(AbstractRuneConfig rune, List<AbstractRuneConfig> tuumRunes,
        TuumValidationResult tuumResult)
    {
        var runeType = rune.GetType();
        int runeIndex = tuumRunes.IndexOf(rune);

        // [InFrontOf] 规则
        if (runeType.GetCustomAttribute<InFrontOfAttribute>() is { } inFrontOfAttr)
        {
            foreach (var targetType in inFrontOfAttr.InFrontOfType)
            {
                int targetIndex = tuumRunes.ToList().FindIndex(m => m.GetType() == targetType);
                if (targetIndex != -1 && runeIndex > targetIndex)
                {
                    this.AddMessageToRune(tuumResult, rune.ConfigId, new ValidationMessage
                    {
                        Severity = RuleSeverity.Warning,
                        Message = $"符文 '{runeType.Name}' 应该在 '{targetType.Name}' 之前执行。",
                        RuleSource = "InFrontOf"
                    });
                }
            }
        }

        // [Behind] 规则
        if (runeType.GetCustomAttribute<BehindAttribute>() is { } behindAttr)
        {
            foreach (var targetType in behindAttr.BehindType)
            {
                int targetIndex = tuumRunes.ToList().FindIndex(m => m.GetType() == targetType);
                if (targetIndex != -1 && runeIndex < targetIndex)
                {
                    this.AddMessageToRune(tuumResult, rune.ConfigId, new ValidationMessage
                    {
                        Severity = RuleSeverity.Warning,
                        Message = $"符文 '{runeType.Name}' 应该在 '{targetType.Name}' 之后执行。",
                        RuleSource = "Behind"
                    });
                }
            }
        }
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

        runeResult.RuneMessages.Add(message);
    }
}