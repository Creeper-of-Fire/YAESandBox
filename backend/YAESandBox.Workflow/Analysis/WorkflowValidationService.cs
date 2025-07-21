using System.Reflection;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Config;

namespace YAESandBox.Workflow.Analysis;

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
    public WorkflowValidationReport Validate(WorkflowProcessorConfig config)
    {
        var report = new WorkflowValidationReport();

        // 初始时，可用的变量池只包含工作流声明的触发参数
        var availableVariables = new HashSet<string>(config.TriggerParams);

        var stepModules = config.Steps
            .Select(s => new { Step = s, Modules = s.Modules })
            .ToList();

        for (int i = 0; i < stepModules.Count; i++)
        {
            var currentStepInfo = stepModules[i];
            var step = currentStepInfo.Step;
            var stepResult = new StepValidationResult();

            // 1. 进行数据流校验 (变量的生产与消费)
            this.ValidateDataFlow(step, availableVariables, stepResult);

            // 2. 进行基于Attribute的拓扑/结构规则校验
            this.ValidateAttributeRules(step, config.Steps, i, stepResult);

            // 如果当前步骤有任何校验信息，就添加到报告中
            if (stepResult.ModuleResults.Any() || stepResult.StepMessages.Any())
            {
                report.StepResults[step.ConfigId] = stepResult;
            }

            // 3. 在处理完一个步骤后，将其产生的全局变量加入可用池，供后续步骤使用
            foreach (string producedVar in step.OutputMappings.Keys)
            {
                availableVariables.Add(producedVar);
            }
        }

        return report;
    }

    /// <summary>
    /// 校验数据流：检查所有模块消费的变量是否都已在之前的步骤、触发参数或本步骤的前置模块中定义。
    /// </summary>
    private void ValidateDataFlow(StepProcessorConfig step, ISet<string> initialAvailableVariables, StepValidationResult stepResult)
    {
        // 1. (保持不变) 检查 InputMappings 的源（Value，即全局变量）是否存在于上游的可用池中
        foreach (string globalName in step.InputMappings.Values.ToHashSet())
        {
            if (!initialAvailableVariables.Contains(globalName))
            {
                stepResult.StepMessages.Add(new ValidationMessage
                {
                    Severity = RuleSeverity.Error,
                    Message = $"输入映射错误：上游未提供可用的全局变量 '{globalName}'。",
                    RuleSource = "DataFlow"
                });
            }
        }

        // 2. 模拟步骤内部的执行流程，动态计算每个模块执行前的可用变量池
        //    这个变量池包含了“外部注入的”和“内部生产的”所有变量。
        var inStepAvailableVars = new HashSet<string>(step.InputMappings.Keys);

        foreach (var module in step.Modules)
        {
            // 2a. 对当前模块进行校验：检查其消费的变量是否都存在于【当前】的可用变量池中
            var requiredVars = module.GetConsumedVariables();
            var missingVars = requiredVars.Except(inStepAvailableVars);

            foreach (string missingVar in missingVars)
            {
                this.AddMessageToModule(stepResult, module.ConfigId, new ValidationMessage
                {
                    Severity = RuleSeverity.Error,
                    Message = $"模块必需的输入变量 '{missingVar}' 未被提供。它既没有在步骤的 InputMappings 中映射，也不是由本步骤中任何前置模块产生的。",
                    RuleSource = "DataFlow"
                });
            }

            // 2b. 更新可用变量池：将当前模块【生产】的变量添加到池中，供后续模块使用
            var producedVars = module.GetProducedVariables();
            foreach (string producedVar in producedVars)
            {
                inStepAvailableVars.Add(producedVar);
            }
        }

        // 3. (新增) 检查 OutputMappings 的源（Value，即步骤内部变量）是否真的被生产出来了
        var allProducedInStepVars = new HashSet<string>(step.Modules.SelectMany(m => m.GetProducedVariables()));
        var allAvailableInStepVars = new HashSet<string>(step.InputMappings.Keys).Union(allProducedInStepVars);

        foreach (var mapping in step.OutputMappings)
        {
            string localSourceVar = mapping.Value; // 局部变量名
            string globalTargetVar = mapping.Key; // 全局变量名

            if (!allAvailableInStepVars.Contains(localSourceVar))
            {
                stepResult.StepMessages.Add(new ValidationMessage
                {
                    Severity = RuleSeverity.Error,
                    Message = $"输出映射错误：尝试将不存在的内部变量 '{localSourceVar}' 映射到全局变量 '{globalTargetVar}'。",
                    RuleSource = "DataFlow"
                });
            }
        }
    }

    /// <summary>
    /// 校验基于Attribute的规则，如[SingleInStep], [InLastStep]等。
    /// </summary>
    private void ValidateAttributeRules(StepProcessorConfig step, List<StepProcessorConfig> allSteps, int currentIndex,
        StepValidationResult stepResult)
    {
        // a. [InLastStep] 校验
        var finalModule = step.Modules.FirstOrDefault(m => m.GetType().GetCustomAttribute<InLastStepAttribute>() != null);
        if (finalModule != null && currentIndex != allSteps.Count - 1)
        {
            // 这是一个步骤级别的错误
            stepResult.StepMessages.Add(new ValidationMessage
            {
                Severity = RuleSeverity.Error,
                Message = $"此步骤包含一个终结模块 ({finalModule.GetType().Name})，但它不是工作流的最后一个步骤。",
                RuleSource = "InLastStep"
            });
        }

        // 遍历模块进行模块级Attribute校验
        foreach (var module in step.Modules)
        {
            var moduleType = module.GetType();

            // b. [SingleInStep] 校验
            if (moduleType.GetCustomAttribute<SingleInStepAttribute>() != null)
            {
                if (step.Modules.Count(m => m.GetType() == moduleType) > 1)
                {
                    this.AddMessageToModule(stepResult, module.ConfigId, new ValidationMessage
                    {
                        // 这通常是一个警告，因为逻辑上可能允许（按顺序执行），但不是最佳实践
                        Severity = RuleSeverity.Warning,
                        Message = $"模块类型 '{moduleType.Name}' 在此步骤中出现了多次，但它被建议只使用一次。",
                        RuleSource = "SingleInStep"
                    });
                }
            }

            // c. [InFrontOf] / [Behind] 校验 (步骤内部顺序)
            this.ValidateRelativeOrder(module, step.Modules, stepResult);
        }
    }

    /// <summary>
    /// 校验单个模块在其步骤内的相对顺序。
    /// </summary>
    private void ValidateRelativeOrder(AbstractModuleConfig module, List<AbstractModuleConfig> stepModules,
        StepValidationResult stepResult)
    {
        var moduleType = module.GetType();
        int moduleIndex = stepModules.IndexOf(module);

        // [InFrontOf] 规则
        if (moduleType.GetCustomAttribute<InFrontOfAttribute>() is { } inFrontOfAttr)
        {
            foreach (var targetType in inFrontOfAttr.InFrontOfType)
            {
                int targetIndex = stepModules.ToList().FindIndex(m => m.GetType() == targetType);
                if (targetIndex != -1 && moduleIndex > targetIndex)
                {
                    this.AddMessageToModule(stepResult, module.ConfigId, new ValidationMessage
                    {
                        Severity = RuleSeverity.Warning,
                        Message = $"模块 '{moduleType.Name}' 应该在 '{targetType.Name}' 之前执行。",
                        RuleSource = "InFrontOf"
                    });
                }
            }
        }

        // [Behind] 规则
        if (moduleType.GetCustomAttribute<BehindAttribute>() is { } behindAttr)
        {
            foreach (var targetType in behindAttr.BehindType)
            {
                int targetIndex = stepModules.ToList().FindIndex(m => m.GetType() == targetType);
                if (targetIndex != -1 && moduleIndex < targetIndex)
                {
                    this.AddMessageToModule(stepResult, module.ConfigId, new ValidationMessage
                    {
                        Severity = RuleSeverity.Warning,
                        Message = $"模块 '{moduleType.Name}' 应该在 '{targetType.Name}' 之后执行。",
                        RuleSource = "Behind"
                    });
                }
            }
        }
    }

    /// <summary>
    /// 辅助方法，安全地向模块的校验结果中添加消息。
    /// </summary>
    private void AddMessageToModule(StepValidationResult stepResult, string moduleId, ValidationMessage message)
    {
        if (!stepResult.ModuleResults.TryGetValue(moduleId, out var moduleResult))
        {
            moduleResult = new ModuleValidationResult();
            stepResult.ModuleResults[moduleId] = moduleResult;
        }

        moduleResult.ModuleMessages.Add(message);
    }
}