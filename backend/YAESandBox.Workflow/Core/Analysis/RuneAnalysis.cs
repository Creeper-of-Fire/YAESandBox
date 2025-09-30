using System.ComponentModel.DataAnnotations;
using YAESandBox.Workflow.Rune.Config;
using YAESandBox.Workflow.VarSpec;

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
    public List<ConsumedSpec> ConsumedVariables { get; init; } = [];

    /// <summary>
    /// 符文生产的输出参数
    /// </summary>
    [Required]
    public List<ProducedSpec> ProducedVariables { get; init; } = [];

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
    /// <returns></returns>
    public RuneAnalysisResult Analyze(AbstractRuneConfig runeConfig)
    {
        return new RuneAnalysisResult
        {
            ConsumedVariables = runeConfig.GetConsumedSpec().ToList(),
            ProducedVariables = runeConfig.GetProducedSpec().ToList(),
            RuneMessages = []
        };
    }
}