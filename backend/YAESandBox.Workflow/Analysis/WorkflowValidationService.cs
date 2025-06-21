using System.Reflection;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Module.ModuleAttribute;

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
            foreach (var producedVar in step.OutputMappings.Keys)
            {
                availableVariables.Add(producedVar);
            }
        }

        return report;
    }

    /// <summary>
    /// 校验数据流：检查所有模块消费的变量是否都已在之前的步骤或触发参数中定义。
    /// </summary>
    private void ValidateDataFlow(StepProcessorConfig step, ISet<string> availableVariables, StepValidationResult stepResult)
    {
        // 1. 检查 InputMappings 的源（Key，即全局变量）是否存在于上游的可用池中
        foreach (string globalName in step.InputMappings.Select(mapping => mapping.Key)
                     .Where(globalName => !availableVariables.Contains(globalName)))
        {
            // 这是一个步骤级别的错误，因为映射的源头就不存在
            stepResult.StepMessages.Add(new ValidationMessage
            {
                Severity = RuleSeverity.Error,
                Message = $"输入映射错误：上游未提供可用的全局变量 '{globalName}'。",
                RuleSource = "DataFlow"
            });
        }

        // 2. 严格检查此步骤的所有模块所需要的输入，是否都已被 InputMappings 的目标（Value，即局部变量）所满足
        var providedLocalVars = new HashSet<string>(step.InputMappings.Values);
        var allRequiredLocalVars = new HashSet<string>(step.Modules.SelectMany(m => m.GetConsumedVariables()));

        // 找出那些需要但未被提供的变量
        var missingVars = allRequiredLocalVars.Except(providedLocalVars);

        foreach (string missingVar in missingVars)
        {
            // 找到所有需要这个缺失变量的模块，并为它们各自添加错误信息
            var consumerModules = step.Modules.Where(m => m.GetConsumedVariables().Contains(missingVar));
            foreach (var module in consumerModules)
            {
                this.AddMessageToModule(stepResult, module.ConfigId, new ValidationMessage
                {
                    Severity = RuleSeverity.Error,
                    Message = $"模块必需的输入变量 '{missingVar}' 未在步骤的 InputMappings 中进行映射。",
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