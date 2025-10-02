using System.ComponentModel.DataAnnotations;
using System.Reflection;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.Config.RuneConfig;
using YAESandBox.Workflow.VarSpec;

namespace YAESandBox.Workflow.WorkflowService.Analysis;

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
    public required List<ConsumedSpec> ConsumedEndpoints { get; init; }

    /// <summary>
    /// Tuum 对外暴露的、可引出连接的【输出端点】的完整定义列表。
    /// 前端可根据此列表生成 Tuum 的输出连接点。
    /// </summary>
    [Required]
    public required List<ProducedSpec> ProducedEndpoints { get; init; }

    /// <summary>
    /// Tuum 内部所有被【消费】的变量的聚合列表。
    /// 这对于前端在配置输入映射时，提供可用的内部目标变量推荐列表非常有用。
    /// </summary>
    [Required]
    public required List<ConsumedSpec> InternalConsumedSpecs { get; init; }

    /// <summary>
    /// Tuum 内部所有被【生产】的变量的聚合列表。
    /// 这对于前端在配置输出映射时，提供可用的内部源变量推荐列表非常有用。
    /// </summary>
    [Required]
    public required List<ProducedSpec> InternalProducedSpecs { get; init; }

    /// <summary>
    /// Tuum 内部所有被发现的变量及其最终推断出的统一类型定义。
    /// Key 是内部变量名，Value 是其类型定义。
    /// 这主要用于内部校验和为外部端点确定类型。
    /// </summary>
    [Required]
    public required Dictionary<string, VarSpecDef> InternalVariableDefinitions { get; init; }

    /// <summary>
    /// 在分析过程中发现的所有错误和警告的列表。
    /// 如果此列表为空，代表 Tuum 配置健康。
    /// </summary>
    [Required]
    public required List<ValidationMessage> Messages { get; init; }
}

/// <summary>
/// 封装了对单个枢机（Tuum）配置进行静态分析和校验的所有逻辑。
/// </summary>
public class TuumAnalysisService
{
    /// <summary>
    /// 对指定的枢机配置进行全面的静态分析和规则校验。
    /// 此方法会调用分部类中的各个方法，并聚合最终的扁平化结果。
    /// </summary>
    /// <param name="tuumConfig">要分析的枢机配置。</param>
    /// <returns>一个包含所有分析和校验信息的扁平化报告。</returns>
    public TuumAnalysisResult Analyze(TuumConfig tuumConfig)
    {
        // 步骤 0: 执行独立的校验和分析
        var tuumMappingUniqueMessages = this.ValidateTuumMappingUniqueness(tuumConfig).ToList();

        // 校验枢机内部所有符文的属性规则
        var runeAttributeMessages = this.ValidateRuneAttributeRules(tuumConfig).ToList();

        // 步骤 1: 确定所有内部变量的类型定义
        var (internalTypeDefs, typeValidationMessages) = this.AnalyzeVariableTypes(tuumConfig);
        var inputTypeValidationMessages = this.AnalyzeInputTypes(tuumConfig, internalTypeDefs);

        // 步骤 2: 进行纯粹的内部需求分析。这是最关键的一步，它不考虑外部输入。
        var internalRequirements = this.AnalyzeInternalRequirements(tuumConfig.GetEnableRunes().ToList());

        // 步骤 3: 使用步骤 2 的结果进行完整的数据流校验。
        var dataFlowMessages = this.ValidateDataFlow(tuumConfig, internalRequirements);

        // 步骤 4: 聚合内部 Spec 定义
        var internalConsumedSpecs = tuumConfig.GetEnableRunes()
            .SelectMany(r => r.GetConsumedSpec())
            .GroupBy(s => s.Name)
            .Select(g => g.First()) // 去重
            .Select(spec => spec with { IsOptional = !internalRequirements.GetValueOrDefault(spec.Name, false) })
            .ToList();

        var internalProducedSpecs = tuumConfig.GetEnableRunes()
            .SelectMany(r => r.GetProducedSpec())
            .GroupBy(s => s.Name)
            .Select(g => g.First()) // 去重
            .ToList();

        // 步骤 4: 使用分析结果生成外部端点定义
        var consumedEndpoints = this.DetermineConsumedEndpoints(tuumConfig, internalTypeDefs, internalRequirements);
        var producedEndpoints = this.DetermineProducedEndpoints(tuumConfig, internalTypeDefs);

        // 聚合所有结果到扁平化的 DTO
        return new TuumAnalysisResult
        {
            InternalConsumedSpecs = internalConsumedSpecs,
            InternalProducedSpecs = internalProducedSpecs,
            InternalVariableDefinitions = internalTypeDefs,
            ConsumedEndpoints = consumedEndpoints,
            ProducedEndpoints = producedEndpoints,
            Messages =
            [
                ..tuumMappingUniqueMessages, ..runeAttributeMessages, ..typeValidationMessages, ..inputTypeValidationMessages,
                ..dataFlowMessages
            ],
        };
    }


    /// <summary>
    /// **分析函数 0: 基础映射校验**
    /// 校验枢机的输入和输出映射是否满足唯一性约束。
    /// </summary>
    private IEnumerable<ValidationMessage> ValidateTuumMappingUniqueness(TuumConfig tuum)
    {
        // 输入的唯一性已经被满足。

        // 输出校验规则：一个外部输出端点只能被一个内部变量驱动 (在所有OutputMappings的Value列表中唯一)
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
    /// 辅助方法，获取类型的别名以生成更友好的消息
    /// </summary>
    private static string getRuneAlias(Type runeType) => runeType.GetCustomAttribute<ClassLabelAttribute>()?.Label ?? runeType.Name;

    /// <summary>
    /// **分析函数 0.5: 符文属性规则校验**
    /// <para>职责：校验枢机内部的所有符文是否满足其Attribute定义的规则（如唯一性、顺序等）。</para>
    /// </summary>
    private IEnumerable<ValidationMessage> ValidateRuneAttributeRules(TuumConfig tuumConfig)
    {
        var tuumRunes = tuumConfig.Runes;

        for (int i = 0; i < tuumRunes.Count; i++)
        {
            var runeConfig = tuumRunes[i];
            var runeType = runeConfig.GetType();

            // 规则: [SingleInTuum]
            if (runeType.GetCustomAttribute<SingleInTuumAttribute>() != null)
            {
                if (tuumRunes.Count(r => r.GetType() == runeType) > 1)
                {
                    yield return new ValidationMessage
                    {
                        Severity = RuleSeverity.Warning,
                        Message = $"规则冲突：符文 '{runeConfig.Name}' (类型: {runeType.Name}) 在此枢机中出现了多次，但它被建议只使用一次。",
                        RuleSource = "SingleInTuum"
                    };
                }
            }

            // 规则: [InFrontOf]
            if (runeType.GetCustomAttribute<InFrontOfAttribute>() is { } inFrontOfAttr)
            {
                foreach (var targetType in inFrontOfAttr.InFrontOfType)
                {
                    int targetIndex = tuumRunes.FindIndex(m => m.GetType() == targetType);
                    if (targetIndex != -1 && i > targetIndex)
                    {
                        yield return new ValidationMessage
                        {
                            Severity = RuleSeverity.Warning,
                            Message = $"顺序警告：符文 '{runeConfig.Name}' 应该在 '{getRuneAlias(targetType)}' 之前执行。",
                            RuleSource = "InFrontOf"
                        };
                    }
                }
            }

            // 规则: [Behind]
            if (runeType.GetCustomAttribute<BehindAttribute>() is { } behindAttr)
            {
                foreach (var targetType in behindAttr.BehindType)
                {
                    int targetIndex = tuumRunes.FindIndex(m => m.GetType() == targetType);
                    if (targetIndex != -1 && i < targetIndex)
                    {
                        yield return new ValidationMessage
                        {
                            Severity = RuleSeverity.Warning,
                            Message = $"顺序警告：符文 '{runeConfig.Name}' 应该在 '{getRuneAlias(targetType)}' 之后执行。",
                            RuleSource = "Behind"
                        };
                    }
                }
            }
        }
    }


    /// <summary>
    /// **分析函数 1: 类型分析**
    /// <para>职责：只关心类型。确定所有内部变量的最终类型，并报告类型冲突。</para>
    /// </summary>
    private (Dictionary<string, VarSpecDef> TypeDefs, List<ValidationMessage>Messages) AnalyzeVariableTypes(TuumConfig config)
    {
        var messages = new List<ValidationMessage>();
        var variableTypeAppearances = new Dictionary<string, List<VarSpecDef>>();

        // 步骤 1.1: 独立遍历，收集每个变量名出现过的所有类型定义。
        foreach (var rune in config.GetEnableRunes())
        {
            foreach (var spec in rune.GetConsumedSpec())
            {
                variableTypeAppearances.TryAdd(spec.Name, []);
                variableTypeAppearances[spec.Name].Add(spec.Def);
            }

            foreach (var spec in rune.GetProducedSpec())
            {
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

        return (finalTypeDefs, messages);
    }

    // 检查映射到同一个外部输入端点的内部变量，其类型是否兼容。
    private IEnumerable<ValidationMessage> AnalyzeInputTypes(TuumConfig config, IReadOnlyDictionary<string, VarSpecDef> typeDefs)
    {
        var endpointTypeConflicts = config.InputMappings
            .GroupBy(kvp => kvp.Value) // 按外部端点名 (Value) 分组
            .Select(group => new
            {
                EndpointName = group.Key,
                // 获取该组内所有内部变量的最终推断类型
                InternalVarTypes = group
                    .Select(kvp => typeDefs.GetValueOrDefault(kvp.Key, CoreVarDefs.Any))
                    .Where(def => def.TypeName != CoreVarDefs.Any.TypeName)
                    .Distinct()
                    .ToList()
            })
            .Where(g => g.InternalVarTypes.Count > 1); // 找出存在多个具体类型的分组

        foreach (var conflict in endpointTypeConflicts)
        {
            yield return new ValidationMessage
            {
                Severity = RuleSeverity.Error,
                Message =
                    $"输入映射冲突：外部端点 '{conflict.EndpointName}' 同时驱动了多个类型不兼容的内部变量。发现了类型: {string.Join(", ", conflict.InternalVarTypes.Select(t => t.TypeName))}",
                RuleSource = "TypeCompatibility"
            };
        }
    }

    /// <summary>
    /// **分析函数 2: 纯粹的内部需求分析**
    /// <para>职责：只关心内部数据流。通过严格按顺序遍历符文，确定在没有外部输入的情况下，哪些变量是真正的“净需求”。</para>
    /// </summary>
    /// <param name="runes">按执行顺序排列的符文列表。</param>
    /// <returns>一个字典，Key是所有被消费过的变量名，Value为 true 代表该变量是净需求。</returns>
    private Dictionary<string, bool> AnalyzeInternalRequirements(IReadOnlyList<AbstractRuneConfig> runes)
    {
        var providedInternally = new HashSet<string>();
        var netRequirements = new HashSet<string>();
        var allConsumedVars = new HashSet<string>();

        // 严格按顺序遍历符文
        foreach (var rune in runes)
        {
            // 步骤 2.1: 首先，检查当前符文的消费
            foreach (var spec in rune.GetConsumedSpec())
            {
                allConsumedVars.Add(spec.Name);

                // 如果一个变量是必需的，并且在当前时间点还没有被内部任何【前置】符文提供，
                // 那么它就是一个净需求。
                if (!spec.IsOptional && !providedInternally.Contains(spec.Name))
                {
                    // 一旦一个变量被确认为净需求，它就永远是净需求。
                    // 任何后续符文对它的生产都无法满足这个在时间上更早的需求。
                    // 因此，我们只添加，绝不移除。
                    netRequirements.Add(spec.Name);
                }
            }

            // 步骤 2.2: 然后，更新当前符文的生产，供【后续】符文使用
            foreach (var spec in rune.GetProducedSpec())
            {
                providedInternally.Add(spec.Name);
            }
        }

        // 步骤 2.3: 构建最终结果字典
        var result = new Dictionary<string, bool>();
        foreach (string varName in allConsumedVars)
        {
            result[varName] = netRequirements.Contains(varName);
        }

        return result;
    }


    /// <summary>
    /// **分析函数 3: 完整的数据流校验**
    /// <para>职责：结合内部需求分析结果和外部映射，检查数据流的完整性和健康度。</para>
    /// </summary>
    private List<ValidationMessage> ValidateDataFlow(TuumConfig config, IReadOnlyDictionary<string, bool> internalRequirements)
    {
        var messages = new List<ValidationMessage>();

        // 步骤 3.1: 找出所有最终的、必需的内部变量（净需求）
        var finalRequiredVars = internalRequirements
            .Where(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .ToHashSet();

        // 步骤 3.2: 获取所有由外部输入提供的变量
        var providedByExternalInputs = config.InputMappings.Keys.ToHashSet();

        // 步骤 3.3: 检查必需输入是否被外部满足
        var unfulfilledVars = finalRequiredVars.Except(providedByExternalInputs);
        foreach (string varName in unfulfilledVars)
        {
            // [Error: 必需输入未提供]
            messages.Add(new ValidationMessage
            {
                Severity = RuleSeverity.Error,
                Message = $"必需的内部变量 '{varName}' 没有数据来源。它需要通过输入映射从外部提供。",
                RuleSource = "DataFlow"
            });
        }

        // 步骤 3.4: 检查冗余和无效映射
        var allConsumedVars = internalRequirements.Keys.ToHashSet();
        var allProducedVars = config.GetEnableRunes().SelectMany(r => r.GetProducedSpec()).Select(s => s.Name).ToHashSet();
        var allOutputMappingSources = config.OutputMappings.Keys.ToHashSet();

        // [Warning: 冗余输入映射]
        // 遍历所有被外部提供的内部变量
        foreach (string internalVar in providedByExternalInputs)
        {
            // 如果这个变量从未被任何符文消费，也未被用于任何输出的源头，则为冗余。
            if (!allConsumedVars.Contains(internalVar) && !allOutputMappingSources.Contains(internalVar))
            {
                messages.Add(new ValidationMessage
                {
                    Severity = RuleSeverity.Warning,
                    Message = $"冗余的输入映射：内部变量 '{internalVar}' 从外部接收了数据，但从未被任何符文消费，也未被用于输出。",
                    RuleSource = "DataFlow"
                });
            }
        }

        // [Warning: 无效的输出映射源]
        foreach (string sourceVar in allOutputMappingSources)
        {
            // 检查源变量是否由任何符文生产 或 由外部输入直接提供
            if (!allProducedVars.Contains(sourceVar) && !providedByExternalInputs.Contains(sourceVar))
            {
                messages.Add(new ValidationMessage
                {
                    Severity = RuleSeverity.Warning,
                    Message = $"无效的输出映射：源变量 '{sourceVar}' 从未被任何内部符文生产过，也未从外部输入获得。此映射可能不会输出任何数据。",
                    RuleSource = "DataFlow"
                });
            }
        }

        return messages;
    }

    /// <summary>
    /// **分析函数 4: 生成输入端点定义**
    /// <para>职责：结合内部需求分析和类型分析，生成枢机的外部输入端点定义。</para>
    /// </summary>
    private List<ConsumedSpec> DetermineConsumedEndpoints(TuumConfig config, IReadOnlyDictionary<string, VarSpecDef> typeDefs,
        IReadOnlyDictionary<string, bool> internalRequirements)
    {
        // 按外部端点名称分组，以生成唯一的端点定义
        var endpointsGrouped = config.InputMappings
            .GroupBy(kvp => kvp.Value); // Key: 外部端点名, Value: IEnumerable of KeyValuePairs

        var consumedEndpoints = new List<ConsumedSpec>();
        foreach (var group in endpointsGrouped)
        {
            string endpointName = group.Key;
            var internalVarsInGroup = group.Select(kvp => kvp.Key).ToHashSet();

            // 确定此端点的可选性：只要它供给的任何一个内部变量是净需求，那么这个端点就是必需的。
            bool isOptional = !internalVarsInGroup.Any(varName => internalRequirements.GetValueOrDefault(varName, false));

            // 确定类型：使用组内第一个内部变量的类型作为代表。
            // (类型兼容性已在 AnalyzeVariableTypes 中校验，这里可以安全地取第一个)
            var endpointDef = CoreVarDefs.Any;
            if (internalVarsInGroup.FirstOrDefault() is { } firstVar && typeDefs.TryGetValue(firstVar, out var def))
            {
                endpointDef = def;
            }

            consumedEndpoints.Add(new ConsumedSpec(endpointName, endpointDef) { IsOptional = isOptional });
        }

        return consumedEndpoints;
    }

    /// <summary>
    /// **分析函数 5: 生成输出端点定义**
    /// <para>职责：只生成枢机的外部输出端点定义。</para>
    /// </summary>
    private List<ProducedSpec> DetermineProducedEndpoints(TuumConfig config, IReadOnlyDictionary<string, VarSpecDef> typeDefs)
    {
        var producedEndpoints = new List<ProducedSpec>();
        foreach ((string sourceVar, var endpointNames) in config.OutputMappings)
        {
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

file static class TuumExpanded
{
    internal static IEnumerable<AbstractRuneConfig> GetEnableRunes(this TuumConfig tuum)
    {
        return tuum.Runes.Where(rune => rune.Enabled);
    }
}