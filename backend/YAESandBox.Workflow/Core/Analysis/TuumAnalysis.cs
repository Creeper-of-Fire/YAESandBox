using System.ComponentModel.DataAnnotations;
using System.Reflection;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Rune;
using YAESandBox.Workflow.Tuum;
using YAESandBox.Workflow.VarSpec;

namespace YAESandBox.Workflow.Core.Analysis;

/// <summary>
/// 对枢机进行静态分析后的结果报告。
/// </summary>
public record TuumAnalysisResult
{
    /// <summary>
    /// Tuum 对外暴露的、可被连接的【输入端点】的完整定义列表。
    /// 前端可根据此列表生成 Tuum 的输入连接点。
    /// </summary>
    [Required]
    public required List<ConsumedSpec> ConsumedEndpoints { get; init; } = [];

    /// <summary>
    /// Tuum 对外暴露的、可引出连接的【输出端点】的完整定义列表。
    /// 前端可根据此列表生成 Tuum 的输出连接点。
    /// </summary>
    [Required]
    public required List<ProducedSpec> ProducedEndpoints { get; init; } = [];

    /// <summary>
    /// Tuum 内部所有被【消费】的变量的聚合列表。
    /// 这对于前端在配置输入映射时，提供可用的内部目标变量推荐列表非常有用。
    /// </summary>
    [Required]
    public required List<ConsumedSpec> InternalConsumedSpecs { get; init; } = [];

    /// <summary>
    /// Tuum 内部所有被【生产】的变量的聚合列表。
    /// 这对于前端在配置输出映射时，提供可用的内部源变量推荐列表非常有用。
    /// </summary>
    [Required]
    public required List<ProducedSpec> InternalProducedSpecs { get; init; } = [];

    /// <summary>
    /// Tuum 内部所有被发现的变量及其最终推断出的统一类型定义。
    /// Key 是内部变量名，Value 是其类型定义。
    /// 这主要用于内部校验和为外部端点确定类型。
    /// </summary>
    [Required]
    public required Dictionary<string, VarSpecDef> InternalVariableDefinitions { get; init; } = [];

    /// <summary>
    /// 在分析过程中发现的所有错误和警告的列表。
    /// 如果此列表为空，代表 Tuum 配置健康。
    /// </summary>
    [Required]
    public List<ValidationMessage> Messages { get; init; } = [];
}

/// <summary>
/// 封装了对单个枢机（Tuum）配置进行静态分析和校验的所有逻辑。
/// </summary>
public partial class TuumAnalysisService
{
    /// <summary>
    /// 对指定的枢机配置进行全面的静态分析和规则校验。
    /// 此方法会调用分部类中的各个方法，并聚合最终的扁平化结果。
    /// </summary>
    /// <param name="tuumConfig">要分析的枢机配置。</param>
    /// <returns>一个包含所有分析和校验信息的扁平化报告。</returns>
    public TuumAnalysisResult Analyze(TuumConfig tuumConfig)
    {
        // 调用分析部分的逻辑
        var tuumMappingUniqueError = this.ValidateTuumMappingUniqueness(tuumConfig).ToList();
        var (internalConsumed, internalProduced, internalTypeDefs, validationTypesMessages) = this.AnalyzeVariableTypes(tuumConfig);
        var dataFlowMessages = this.ValidateDataFlow(tuumConfig);

        var consumedEndpoints = this.DetermineConsumedEndpoints(tuumConfig, internalTypeDefs);
        var producedEndpoints = this.DetermineProducedEndpoints(tuumConfig, internalTypeDefs);

        // 聚合所有结果到扁平化的 DTO
        return new TuumAnalysisResult
        {
            InternalConsumedSpecs = internalConsumed,
            InternalProducedSpecs = internalProduced,
            InternalVariableDefinitions = internalTypeDefs,
            ConsumedEndpoints = consumedEndpoints,
            ProducedEndpoints = producedEndpoints,
            Messages = [..tuumMappingUniqueError, ..validationTypesMessages, ..dataFlowMessages],
        };
    }

    /// <summary>
    /// **分析函数 0: 基础映射校验**
    /// 校验枢机的输入和输出映射是否满足唯一性约束。
    /// </summary>
    private IEnumerable<ValidationMessage> ValidateTuumMappingUniqueness(TuumConfig tuum)
    {
        // 校验规则1：一个内部变量只能有一个数据源 (在所有InputMappings的Value列表中唯一)
        var allTargetInternalVars = tuum.InputMappings.Values.SelectMany(v => v);
        var duplicateInternalVars = allTargetInternalVars
            .GroupBy(v => v)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (string duplicateVar in duplicateInternalVars)
        {
            yield return new ValidationMessage
            {
                Severity = RuleSeverity.Error,
                Message = $"输入映射冲突：内部变量 '{duplicateVar}' 被多个外部输入端点驱动，它只能有一个数据源。",
                RuleSource = "MappingUniqueness"
            };
        }

        // 校验规则2：一个外部输出端点只能被一个内部变量驱动 (在所有OutputMappings的Value列表中唯一)
        var allTargetExternalEndpoints = tuum.OutputMappings.Values.SelectMany(v => v);
        var duplicateExternalEndpoints = allTargetExternalEndpoints
            .GroupBy(e => e)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (string duplicateEndpoint in duplicateExternalEndpoints)
        {
            yield return new ValidationMessage
            {
                Severity = RuleSeverity.Error,
                Message = $"输出映射冲突：外部端点 '{duplicateEndpoint}' 被多个内部变量驱动，它只能有一个数据源。",
                RuleSource = "MappingUniqueness"
            };
        }
    }

    /// <summary>
    /// **分析函数 1: 类型分析**
    /// <para>职责：只关心类型。确定所有内部变量的最终类型，并报告类型冲突。</para>
    /// </summary>
    private (List<ConsumedSpec> Consumed, List<ProducedSpec> Produced,
        Dictionary<string, VarSpecDef> TypeDefs, List<ValidationMessage>Messages)
        AnalyzeVariableTypes(TuumConfig config)
    {
        var messages = new List<ValidationMessage>();
        var internalConsumed = new List<ConsumedSpec>();
        var internalProduced = new List<ProducedSpec>();
        var variableTypeAppearances = new Dictionary<string, List<VarSpecDef>>();

        // 步骤 1.1: 独立遍历，收集每个变量名出现过的所有类型定义。
        foreach (var rune in config.Runes)
        {
            foreach (var spec in rune.GetConsumedSpec())
            {
                internalConsumed.Add(spec);
                variableTypeAppearances.TryAdd(spec.Name, []);
                variableTypeAppearances[spec.Name].Add(spec.Def);
            }

            foreach (var spec in rune.GetProducedSpec())
            {
                internalProduced.Add(spec);
                variableTypeAppearances.TryAdd(spec.Name, []);
                variableTypeAppearances[spec.Name].Add(spec.Def);
            }
        }

        var finalTypeDefs = new Dictionary<string, VarSpecDef>();

        // 步骤 1.2: 对每个变量，聚合其类型并检查兼容性。
        foreach ((string varName, var appearances) in variableTypeAppearances)
        {
            VarSpecDef? finalDef;
            // 找出所有非 Any 的类型定义
            var concreteTypes = appearances.Where(def => def.TypeName != CoreVarDefs.Any.TypeName).Distinct().ToList();

            if (concreteTypes.Count > 1)
            {
                // [Error 1: 类型不兼容]
                messages.Add(new ValidationMessage
                {
                    Severity = RuleSeverity.Error,
                    Message = $"内部变量 '{varName}' 的类型定义不兼容。发现了多种具体类型: {string.Join(", ", concreteTypes.Select(t => t.TypeName))}",
                    RuleSource = "TypeCompatibility"
                });
                // 当存在冲突时，我们仍然选择一个作为代表（比如第一个），或者干脆标记为 Any，以便后续流程能继续。这里我们选择Any。
                finalDef = CoreVarDefs.Any;
            }
            else
            {
                // 如果只有一个具体类型或全是Any，则类型兼容。
                finalDef = concreteTypes.FirstOrDefault() ?? CoreVarDefs.Any;
            }

            finalTypeDefs[varName] = finalDef;
        }

        return (internalConsumed, internalProduced, finalTypeDefs, messages);
    }

    /// <summary>
    /// **分析函数 2: 数据流校验**
    /// <para>职责：只关心数据流。检查必需输入是否满足，以及是否存在冗余映射。完全不关心类型。</para>
    /// </summary>
    private List<ValidationMessage> ValidateDataFlow(TuumConfig config)
    {
        var messages = new List<ValidationMessage>();

        // 步骤 2.1: 独立遍历，识别所有“必需”的变量。
        var requiredVars = new HashSet<string>();
        foreach (var rune in config.Runes)
        {
            foreach (var spec in rune.GetConsumedSpec().Where(s => !s.IsOptional))
            {
                requiredVars.Add(spec.Name);
            }
        }

        // 步骤 2.2: 独立遍历，识别所有“已提供”的变量。
        var providedVars = new HashSet<string>();
        foreach (var rune in config.Runes)
        {
            foreach (var spec in rune.GetProducedSpec())
            {
                providedVars.Add(spec.Name);
            }
        }

        // 从外部注入的也算已提供
        foreach (string internalVar in config.InputMappings.Values.SelectMany(v => v))
        {
            providedVars.Add(internalVar);
        }

        // 步骤 2.3: 检查必需输入是否满足。
        var unfulfilledVars = requiredVars.Except(providedVars);
        foreach (string varName in unfulfilledVars)
        {
            // [Error 2: 必需输入未提供]
            messages.Add(new ValidationMessage
            {
                Severity = RuleSeverity.Error,
                Message = $"必需的内部变量 '{varName}' 没有数据来源。它需要被内部的符文生产，或通过输入映射从外部提供。",
                RuleSource = "DataFlow"
            });
        }

        // 步骤 2.4: 独立遍历，检查冗余和无效映射。
        var allConsumedVars = config.Runes.SelectMany(r => r.GetConsumedSpec()).Select(s => s.Name).ToHashSet();
        var allProducedVars = config.Runes.SelectMany(r => r.GetProducedSpec()).Select(s => s.Name).ToHashSet();
        var allOutputMappingSources = config.OutputMappings.Keys.ToHashSet();

        // [Warning 1: 冗余输入映射]
        foreach (string internalVar in config.InputMappings.Values.SelectMany(v => v))
        {
            if (!allConsumedVars.Contains(internalVar) && !allOutputMappingSources.Contains(internalVar))
            {
                messages.Add(new ValidationMessage
                {
                    Severity = RuleSeverity.Warning,
                    Message = $"冗余的输入映射：由外部提供的内部变量 '{internalVar}' 在此枢机中从未被任何符文消费，也未被用于输出。",
                    RuleSource = "DataFlow"
                });
            }
        }

        // [Warning 2: 输出映射源不存在]
        foreach (string sourceVar in allOutputMappingSources)
        {
            if (!allProducedVars.Contains(sourceVar))
            {
                messages.Add(new ValidationMessage
                {
                    Severity = RuleSeverity.Warning,
                    Message = $"无效的输出映射：源变量 '{sourceVar}' 从未被任何内部符文生产过。此映射可能不会输出任何数据。",
                    RuleSource = "DataFlow"
                });
            }
        }

        return messages;
    }

    /// <summary>
    /// **分析函数 3: 生成输入端点定义**
    /// <para>职责：只生成枢机的外部输入端点定义。</para>
    /// </summary>
    private List<ConsumedSpec> DetermineConsumedEndpoints(TuumConfig config, IReadOnlyDictionary<string, VarSpecDef> typeDefs)
    {
        // 步骤 3.1: 独立遍历，再次识别所有“必需”的变量。
        var requiredInternalVars = new HashSet<string>();
        foreach (var rune in config.Runes)
        {
            foreach (var spec in rune.GetConsumedSpec().Where(s => !s.IsOptional))
            {
                requiredInternalVars.Add(spec.Name);
            }
        }

        var consumedEndpoints = new List<ConsumedSpec>();
        // 步骤 3.2: 独立遍历 InputMappings，构建端点定义。
        foreach ((string endpointName, var internalVars) in config.InputMappings)
        {
            // 确定此端点的可选性：只要它供给的任何一个内部变量是必需的，那么这个端点就是必需的。
            bool isOptional = !internalVars.Any(requiredInternalVars.Contains);

            // 确定此端点的类型：应该是其供给的所有内部变量的共同类型。
            // 在这一步我们假设类型已在 AnalyzeVariableTypes 中被校验为兼容，因此只需取第一个的类型即可。
            // 如果没有内部变量映射，则默认为 Any。
            var endpointDef = CoreVarDefs.Any;
            if (internalVars.FirstOrDefault() is { } firstVar && typeDefs.TryGetValue(firstVar, out var def))
            {
                endpointDef = def;
            }

            consumedEndpoints.Add(new ConsumedSpec(endpointName, endpointDef) { IsOptional = isOptional });
        }

        return consumedEndpoints;
    }

    /// <summary>
    /// **分析函数 4: 生成输出端点定义**
    /// <para>职责：只生成枢机的外部输出端点定义。</para>
    /// </summary>
    private List<ProducedSpec> DetermineProducedEndpoints(TuumConfig config, IReadOnlyDictionary<string, VarSpecDef> typeDefs)
    {
        var producedEndpoints = new List<ProducedSpec>();

        // 步骤 4.1: 独立遍历 OutputMappings，构建端点定义。
        foreach ((string sourceVar, var endpointNames) in config.OutputMappings)
        {
            // 查找源变量的类型。如果找不到，默认为 Any。
            typeDefs.TryGetValue(sourceVar, out var sourceDef);
            sourceDef ??= CoreVarDefs.Any;

            foreach (string endpointName in endpointNames)
            {
                producedEndpoints.Add(new ProducedSpec(endpointName, sourceDef));
            }
        }

        return producedEndpoints;
    }
}