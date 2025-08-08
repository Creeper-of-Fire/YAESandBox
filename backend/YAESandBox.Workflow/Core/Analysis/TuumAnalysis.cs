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
        // 步骤 0: 执行独立的校验和分析
        var tuumMappingUniqueMessages = this.ValidateTuumMappingUniqueness(tuumConfig).ToList();
        var (internalTypeDefs, typeValidationMessages) = this.AnalyzeVariableTypes(tuumConfig);

        // 步骤 1: 进行纯粹的内部需求分析。这是最关键的一步，它不考虑外部输入。
        var internalRequirements = this.AnalyzeInternalRequirements(tuumConfig.Runes);

        // 步骤 2: 使用步骤 1 的结果进行完整的数据流校验。
        var dataFlowMessages = this.ValidateDataFlow(tuumConfig, internalRequirements);

        // 步骤 3: 聚合内部 Spec 定义
        var internalConsumedSpecs = tuumConfig.Runes
            .SelectMany(r => r.GetConsumedSpec())
            .GroupBy(s => s.Name)
            .Select(g => g.First()) // 去重
            .Select(spec => spec with { IsOptional = !internalRequirements.GetValueOrDefault(spec.Name, false) })
            .ToList();

        var internalProducedSpecs = tuumConfig.Runes
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
            Messages = [..tuumMappingUniqueMessages, ..typeValidationMessages, ..dataFlowMessages],
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
    private (Dictionary<string, VarSpecDef> TypeDefs, List<ValidationMessage>Messages) AnalyzeVariableTypes(TuumConfig config)
    {
        var messages = new List<ValidationMessage>();
        var variableTypeAppearances = new Dictionary<string, List<VarSpecDef>>();

        // 步骤 1.1: 独立遍历，收集每个变量名出现过的所有类型定义。
        foreach (var rune in config.Runes)
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
        var providedByExternalInputs = config.InputMappings.Values.SelectMany(v => v).ToHashSet();

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
        var allConsumedVars = config.Runes.SelectMany(r => r.GetConsumedSpec()).Select(s => s.Name).ToHashSet();
        var allProducedVars = config.Runes.SelectMany(r => r.GetProducedSpec()).Select(s => s.Name).ToHashSet();
        var allOutputMappingSources = config.OutputMappings.Keys.ToHashSet();

        // [Warning: 冗余输入映射]
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
        var consumedEndpoints = new List<ConsumedSpec>();
        foreach ((string endpointName, var internalVars) in config.InputMappings)
        {
            // 确定此端点的可选性：只要它供给的任何一个内部变量是净需求，那么这个端点就是必需的。
            bool isOptional = !internalVars.Any(varName => internalRequirements.GetValueOrDefault(varName, false));

            // 确定类型
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