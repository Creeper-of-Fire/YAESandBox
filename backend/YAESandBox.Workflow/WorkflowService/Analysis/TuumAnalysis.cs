using System.ComponentModel.DataAnnotations;
using System.Reflection;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.Core.Config;
using YAESandBox.Workflow.Core.Config.RuneConfig;
using YAESandBox.Workflow.Core.VarSpec;
using YAESandBox.Workflow.Schema;

namespace YAESandBox.Workflow.WorkflowService.Analysis;

/// <summary>
/// 定义Tuum静态分析期间的类型兼容性检查模式。
/// </summary>
public enum TypeAnalysisMode
{
    /// <summary>
    /// **(最宽松)** 只比较顶层类型名称，完全忽略记录(Record)的内部结构。
    /// 这是最快的模式，但无法发现结构性错误。
    /// </summary>
    IgnoreStructure,

    /// <summary>
    /// **(推荐)** 结构化类型（鸭子类型）检查。
    /// 如果类型A的结构包含了类型B所需的所有属性，并且这些属性的类型也兼容，则认为A兼容B。
    /// 允许类型A有额外的属性。这是最灵活且实用的模式。
    /// <example>一个拥有 {Name, Description, Level} 的 `Character` 类型，可以兼容一个只需要 {Name, Description} 的 `ThingInfo` 类型。</example>
    /// </summary>
    DuckTyping,

    /// <summary>
    /// **(最严格)** 严格的具名类型检查。
    /// 同时也会进行结构检查。
    /// 两个记录(Record)类型只有在它们的类型名称完全相同时才被认为是兼容的，即使它们的结构一模一样。
    /// </summary>
    StrictNominal
}

/// <summary>
/// 封装了类型兼容性检查的结果。
/// </summary>
/// <param name="IsSuccess">检查是否成功。</param>
/// <param name="FailureReason">如果失败，提供具体原因。</param>
public record CompatibilityResult(bool IsSuccess, string? FailureReason = null)
{
    /// <summary>
    /// 表示兼容的静态实例。
    /// </summary>
    public static CompatibilityResult Success { get; } = new(true);

    /// <summary>
    /// 创建一个表示不兼容的结果。
    /// </summary>
    /// <param name="reason">不兼容的原因。</param>
    /// <returns>一个包含原因的不兼容结果。</returns>
    public static CompatibilityResult Fail(string reason) => new(false, reason);
}

/// <summary>
/// 为 VarSpecDef 提供类型兼容性检查的扩展方法。
/// </summary>
internal static class VarSpecDefExtensions
{
    /// <summary>
    /// 递归地检查当前类型定义是否与目标类型定义兼容，并返回详细结果。
    /// </summary>
    /// <param name="sourceDef">源类型（提供方）。</param>
    /// <param name="targetDef">目标类型（需求方）。</param>
    /// <param name="mode">要使用的分析模式。</param>
    /// <returns>一个包含兼容性结果和失败原因的对象。</returns>
    public static CompatibilityResult CheckCompatibilityWith(this VarSpecDef sourceDef, VarSpecDef targetDef, TypeAnalysisMode mode)
    {
        // 规则 0: Any 类型与任何类型都兼容。
        if (sourceDef.TypeName == CoreVarDefs.Any.TypeName || targetDef.TypeName == CoreVarDefs.Any.TypeName)
        {
            return CompatibilityResult.Success;
        }

        // 规则 1: 在 IgnoreStructure 模式下，我们只关心顶层 TypeName 是否相同。
        if (mode == TypeAnalysisMode.IgnoreStructure)
        {
            return sourceDef.TypeName == targetDef.TypeName
                ? CompatibilityResult.Success
                : CompatibilityResult.Fail($"在 '{mode}' 模式下，类型名称不匹配: '{sourceDef.TypeName}' vs '{targetDef.TypeName}'。");
        }

        // 规则2: 如果类型不匹配（一个List，一个Record），则不兼容。
        if (sourceDef.GetType() != targetDef.GetType())
        {
            return CompatibilityResult.Fail($"基础类型种类不匹配: 一个是 {sourceDef.GetType().Name}，另一个是 {targetDef.GetType().Name}。");
        }

        // 根据具体类型进行分派
        return (sourceDef, targetDef) switch
        {
            (PrimitiveVarSpecDef s, PrimitiveVarSpecDef t) =>
                s.TypeName == t.TypeName
                    ? CompatibilityResult.Success
                    : CompatibilityResult.Fail($"基础类型不匹配: 期望 '{t.TypeName}'，但实际是 '{s.TypeName}'。"),


            (ListVarSpecDef s, ListVarSpecDef t) =>
                CheckListCompatibility(s, t, mode),

            (RecordVarSpecDef s, RecordVarSpecDef t) =>
                CheckRecordCompatibility(s, t, mode),

            _ => CompatibilityResult.Fail("未知的类型组合进行比较。") // 未知类型组合
        };
    }

    private static CompatibilityResult CheckListCompatibility(ListVarSpecDef s, ListVarSpecDef t, TypeAnalysisMode mode)
    {
        // 列表兼容性取决于其元素类型的兼容性。
        var elementResult = s.ElementDef.CheckCompatibilityWith(t.ElementDef, mode);
        return elementResult.IsSuccess
            ? CompatibilityResult.Success
            : CompatibilityResult.Fail($"列表元素类型不兼容: {elementResult.FailureReason}");
    }

    private static CompatibilityResult CheckRecordCompatibility(RecordVarSpecDef s, RecordVarSpecDef t, TypeAnalysisMode mode)
    {
        // 严格模式下，首先检查类型名
        if (mode == TypeAnalysisMode.StrictNominal && s.TypeName != t.TypeName)
        {
            return CompatibilityResult.Fail($"在 '{mode}' 模式下，记录类型名称必须完全相同，期望 '{t.TypeName}'，但实际是 '{s.TypeName}'。");
        }

        // 对目标记录的每个必需属性进行检查
        foreach (var targetProp in t.Properties)
        {
            // 检查源记录中是否存在该属性
            if (!s.Properties.TryGetValue(targetProp.Key, out var sourceProp))
            {
                return CompatibilityResult.Fail($"源记录 '{s.TypeName}' 缺少目标 '{t.TypeName}' 所需的属性 '{targetProp.Key}'。");
            }

            // 递归检查属性的类型兼容性
            var propCompatibility = sourceProp.CheckCompatibilityWith(targetProp.Value, mode);
            if (!propCompatibility.IsSuccess)
            {
                return CompatibilityResult.Fail($"属性 '{targetProp.Key}' 的类型不兼容: {propCompatibility.FailureReason}");
            }
        }

        return CompatibilityResult.Success;
    }
}

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
    /// Tuum 内部所有符文声明的、将向外部发射的事件的静态契约列表。
    /// 这描述了 Tuum 的所有副作用。
    /// </summary>
    [Required]
    public required List<EmittedEventSpec> EmittedEvents { get; init; }

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
    /// <param name="mode">用于类型检查的分析模式。</param>
    /// <returns>一个包含所有分析和校验信息的扁平化报告。</returns>
    public TuumAnalysisResult Analyze(TuumConfig tuumConfig, TypeAnalysisMode mode = TypeAnalysisMode.DuckTyping)
    {
        var enabledRunes = tuumConfig.GetEnableRunes().ToList();
        // 步骤 0: 执行独立的校验和分析
        var tuumMappingUniqueMessages = this.ValidateTuumMappingUniqueness(tuumConfig).ToList();

        // 校验枢机内部所有符文的属性规则
        var runeAttributeMessages = this.ValidateRuneAttributeRules(tuumConfig).ToList();

        // 步骤 1: 外部API契约分析 (确定哪些输入是必需的)
        var internalRequirements = this.AnalyzeInternalRequirements(enabledRunes.ToList());

        // 步骤 2: 顺序数据流、类型和副作用分析
        var (flowAndTypeMessages, finalTypeDefs, allConsumedVars, allProducedVars,emittedEvents) =
            this.AnalyzeSequentialDataFlowAndTypes(tuumConfig, enabledRunes, mode);

        // 步骤 3: 外部映射的健全性校验 (依赖步骤1和2的结果)
        var dataFlowMessages = this.ValidateExternalMappings(tuumConfig, internalRequirements, allConsumedVars, allProducedVars);

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
        var consumedEndpoints = this.DetermineConsumedEndpoints(tuumConfig, finalTypeDefs, internalRequirements);
        var producedEndpoints = this.DetermineProducedEndpoints(tuumConfig, finalTypeDefs);

        // 聚合所有结果到扁平化的 DTO
        return new TuumAnalysisResult
        {
            InternalConsumedSpecs = internalConsumedSpecs,
            InternalProducedSpecs = internalProducedSpecs,
            InternalVariableDefinitions = finalTypeDefs,
            ConsumedEndpoints = consumedEndpoints,
            ProducedEndpoints = producedEndpoints,
            EmittedEvents = emittedEvents,
            Messages =
            [
                ..tuumMappingUniqueMessages, ..runeAttributeMessages, ..flowAndTypeMessages, ..dataFlowMessages,
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
    private static string GetRuneAlias(Type runeType) => runeType.GetCustomAttribute<ClassLabelAttribute>()?.Label ?? runeType.Name;

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
                            Message = $"顺序警告：符文 '{runeConfig.Name}' 应该在 '{GetRuneAlias(targetType)}' 之前执行。",
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
                            Message = $"顺序警告：符文 '{runeConfig.Name}' 应该在 '{GetRuneAlias(targetType)}' 之后执行。",
                            RuleSource = "Behind"
                        };
                    }
                }
            }
        }
    }

    /// <summary>
    /// **核心分析函数**: 按顺序模拟数据流，检查连通性和类型兼容性。
    /// </summary>
    private (
        List<ValidationMessage> Messages,
        Dictionary<string, VarSpecDef> FinalTypeDefs,
        HashSet<string> AllConsumedVars,
        HashSet<string> AllProducedVars,
        List<EmittedEventSpec> AllEmittedEvents
        )
        AnalyzeSequentialDataFlowAndTypes(TuumConfig tuumConfig, IReadOnlyList<AbstractRuneConfig> enabledRunes, TypeAnalysisMode mode)
    {
        var messages = new List<ValidationMessage>();
        var currentVariableTypes = new Dictionary<string, VarSpecDef>();
        var allConsumedVars = new HashSet<string>();
        var allProducedVars = new HashSet<string>(tuumConfig.InputMappings.Keys); // 外部输入是最初的生产者
        var allEmittedEvents = new List<EmittedEventSpec>();

        // 初始化：外部输入提供了初始类型。我们用一个占位符，因为输入的真实类型由第一个消费者决定。
        foreach (string inputVar in tuumConfig.InputMappings.Keys)
        {
            // 输入的类型是动态的，只有当它被消费时我们才能检查。这里我们标记为Any。
            currentVariableTypes[inputVar] = CoreVarDefs.Any;
        }

        // 严格按顺序遍历符文
        foreach (var rune in enabledRunes)
        {
            // 步骤 1: 检查当前符文的消费
            foreach (var consumedSpec in rune.GetConsumedSpec())
            {
                allConsumedVars.Add(consumedSpec.Name);

                // 检查数据源是否存在
                if (!allProducedVars.Contains(consumedSpec.Name))
                {
                    if (!consumedSpec.IsOptional)
                    {
                        messages.Add(new ValidationMessage
                        {
                            Severity = RuleSeverity.Error,
                            Message = $"数据流中断：符文 '{rune.Name}' 需要的变量 '{consumedSpec.Name}' 在此执行点之前从未被生产过，也未从外部输入。",
                            RuleSource = "DataFlow"
                        });
                    }

                    continue; // 无法进行类型检查，跳过
                }

                // 进行类型兼容性检查
                var sourceDef = currentVariableTypes[consumedSpec.Name];
                var targetDef = consumedSpec.Def;

                var compatibilityResult = sourceDef.CheckCompatibilityWith(targetDef, mode);
                if (!compatibilityResult.IsSuccess)
                {
                    messages.Add(new ValidationMessage
                    {
                        Severity = RuleSeverity.Error,
                        Message = $"类型不匹配：符文 '{rune.Name}' 消费的变量 '{consumedSpec.Name}' 类型不兼容。" +
                                  $" 当前可用类型是 '{sourceDef.TypeName}'，但符文需要兼容 '{targetDef.TypeName}' 的类型。原因: {compatibilityResult.FailureReason}",
                        RuleSource = "TypeCompatibility"
                    });
                }
            }

            // 步骤 1.5: 分析当前符文的副作用
            // 我们将【执行此Rune之前】的类型上下文传递给它。
            // 因为Rune的副作用通常是基于它所消费的数据。
            var eventsFromRune = rune.AnalyzeEmittedEvents(currentVariableTypes);
            allEmittedEvents.AddRange(eventsFromRune);

            // 步骤 2: 更新当前符文的生产
            foreach (var producedSpec in rune.GetProducedSpec())
            {
                // 更新或添加变量的当前类型
                currentVariableTypes[producedSpec.Name] = producedSpec.Def;
                allProducedVars.Add(producedSpec.Name);
            }
        }

        return (messages, currentVariableTypes, allConsumedVars, allProducedVars, allEmittedEvents);
    }

    /// <summary>
    /// **分析函数**: 校验外部映射的完整性和健康度。
    /// </summary>
    private List<ValidationMessage> ValidateExternalMappings(
        TuumConfig config,
        IReadOnlyDictionary<string, bool> internalRequirements,
        IReadOnlySet<string> allConsumedVars,
        IReadOnlySet<string> allProducedVars)
    {
        var messages = new List<ValidationMessage>();

        // 检查必需输入是否被满足
        var finalRequiredVars = internalRequirements.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToHashSet();
        var providedByExternalInputs = config.InputMappings.Keys.ToHashSet();
        var unfulfilledVars = finalRequiredVars.Except(providedByExternalInputs);
        foreach (string varName in unfulfilledVars)
        {
            messages.Add(new ValidationMessage
                { Severity = RuleSeverity.Error, Message = $"必需的内部变量 '{varName}' 没有数据来源。它需要通过输入映射从外部提供。", RuleSource = "DataFlow" });
        }

        // 检查冗余输入映射
        var allOutputMappingSources = config.OutputMappings.Keys;
        foreach (string internalVar in providedByExternalInputs)
        {
            if (!allConsumedVars.Contains(internalVar) && !allOutputMappingSources.Contains(internalVar))
            {
                messages.Add(new ValidationMessage
                {
                    Severity = RuleSeverity.Warning, Message = $"冗余的输入映射：内部变量 '{internalVar}' 从外部接收了数据，但从未被任何符文消费，也未被用于输出。",
                    RuleSource = "DataFlow"
                });
            }
        }

        // 检查无效的输出映射源
        foreach (string sourceVar in allOutputMappingSources)
        {
            if (!allProducedVars.Contains(sourceVar))
            {
                messages.Add(new ValidationMessage
                {
                    Severity = RuleSeverity.Warning, Message = $"无效的输出映射：源变量 '{sourceVar}' 从未被任何内部符文生产过，也未从外部输入获得。此映射可能不会输出任何数据。",
                    RuleSource = "DataFlow"
                });
            }
        }

        return messages;
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
            VarSpecDef endpointDef = CoreVarDefs.Any;
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