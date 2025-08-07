using System.ComponentModel.DataAnnotations;
using System.Reflection;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Rune;
using YAESandBox.Workflow.Tuum;

namespace YAESandBox.Workflow.Core.Analysis;

/// <summary>
/// 单个符文的校验结果。
/// </summary>
public record RuneAnalysisResult
{
    /// <summary>
    /// 符文消费的输入参数
    /// </summary>
    [Required]
    public List<string> ConsumedVariables { get; init; } = [];

    /// <summary>
    /// 符文生产的输出参数
    /// </summary>
    [Required]
    public List<string> ProducedVariables { get; init; } = [];

    /// <summary>
    /// 针对该符文的校验信息列表。
    /// </summary>
    [Required]
    public List<ValidationMessage> RuneMessages { get; init; } = [];
}

/// <summary>
/// 提供对单个符文（Rune）配置进行规则校验的服务。
/// </summary>
public class RuneAnalysisService
{
    /// <summary>
    /// 对指定的符文配置进行分析/校验。
    /// </summary>
    /// <param name="runeConfig">要校验的符文配置。</param>
    /// <param name="tuumConfig">作为上下文的枢机配置。如果没有，则在无上下文情况下只输出其变量定义。</param>
    /// <returns></returns>
    public RuneAnalysisResult Analyze(
        AbstractRuneConfig runeConfig, TuumConfig? tuumConfig)
    {
        // 校验 1: 枢机内部的符文 Attribute 规则 (如 SingleInTuum, InFrontOf)
        var inTuumResult = tuumConfig is not null
            ? this.ValidateInTuumRuneAttributeRules(runeConfig, tuumConfig)
            : [];

        return new RuneAnalysisResult
        {
            ConsumedVariables = runeConfig.GetConsumedSpec().Select(spec => spec.Name).ToList(),
            ProducedVariables = runeConfig.GetProducedSpec().Select(spec => spec.Name).ToList(),
            RuneMessages = inTuumResult.ToList()
        };
    }

    /// <summary>
    /// 校验枢机内部的符文是否满足其Attribute定义的规则。
    /// </summary>
    private IEnumerable<ValidationMessage> ValidateInTuumRuneAttributeRules(AbstractRuneConfig runeConfig, TuumConfig tuumConfig)
    {
        var runeType = runeConfig.GetType();
        var tuumRunes = tuumConfig.Runes;
        int runeIndex = tuumRunes.ToList().FindIndex(r => r.ConfigId == runeConfig.ConfigId);

        // 规则: [SingleInTuum]
        if (runeType.GetCustomAttribute<SingleInTuumAttribute>() != null)
        {
            if (tuumRunes.Count(r => r.GetType() == runeType) > 1)
            {
                yield return new ValidationMessage
                {
                    Severity = RuleSeverity.Warning,
                    Message = $"规则冲突：符文类型 '{runeType.Name}' 在此枢机中出现了多次，但它被建议只使用一次。",
                    RuleSource = "SingleInTuum"
                };
            }
        }

        // 规则: [InFrontOf]
        if (runeType.GetCustomAttribute<InFrontOfAttribute>() is { } inFrontOfAttr)
        {
            foreach (var targetType in from targetType in inFrontOfAttr.InFrontOfType
                     let targetIndex = tuumRunes.FindIndex(m => m.GetType() == targetType)
                     where targetIndex != -1 && runeIndex > targetIndex
                     select targetType)
            {
                yield return new ValidationMessage
                {
                    Severity = RuleSeverity.Warning,
                    Message = $"顺序警告：符文 '{runeType.Name}' 应该在 '{targetType.Name}' 之前执行。",
                    RuleSource = "InFrontOf"
                };
            }
        }

        // 规则: [Behind]
        if (runeType.GetCustomAttribute<BehindAttribute>() is { } behindAttr)
        {
            foreach (var targetType in from targetType in behindAttr.BehindType
                     let targetIndex = tuumRunes.FindIndex(m => m.GetType() == targetType)
                     where targetIndex != -1 && runeIndex < targetIndex
                     select targetType)
            {
                yield return new ValidationMessage
                {
                    Severity = RuleSeverity.Warning,
                    Message = $"顺序警告：符文 '{runeType.Name}' 应该在 '{targetType.Name}' 之后执行。",
                    RuleSource = "Behind"
                };
            }
        }
    }
}