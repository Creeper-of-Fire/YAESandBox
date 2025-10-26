using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Results;
using YAESandBox.Workflow.Core.Config.RuneConfig;
using YAESandBox.Workflow.Core.Runtime.WorkflowService;
using YAESandBox.Workflow.Core.VarSpec;
using YAESandBox.Workflow.ExactRune;

namespace YAESandBox.Workflow.WorkflowService.Analysis;

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
public class RuneAnalysisService(WorkflowConfigFindService findService)
{
    private WorkflowConfigFindService FindService { get; } = findService;

    /// <summary>
    /// 对指定的符文配置进行分析/校验。
    /// </summary>
    /// <param name="runeConfig">要分析的符文配置。</param>
    /// <param name="userId">执行此操作的用户的ID。</param>
    /// <returns>包含符文输入、输出和校验消息的分析结果。</returns>
    public async Task<RuneAnalysisResult> AnalyzeAsync(AbstractRuneConfig runeConfig, string userId)
    {
        // --- 核心改造：处理引用符文 ---
        if (runeConfig is ReferenceRuneConfig refConfig)
        {
            return await this.AnalyzeReferenceRuneAsync(refConfig, userId);
        }

        // --- 默认逻辑：处理普通符文 ---
        return new RuneAnalysisResult
        {
            ConsumedVariables = runeConfig.GetConsumedSpec(),
            ProducedVariables = runeConfig.GetProducedSpec(),
            RuneMessages = [] // 未来可以添加通用的校验逻辑等
        };
    }

    /// <summary>
    /// 专门用于分析 ReferenceRuneConfig 的私有方法。
    /// </summary>
    private async Task<RuneAnalysisResult> AnalyzeReferenceRuneAsync(ReferenceRuneConfig refConfig, string userId)
    {
        // Case 1: 引用配置不完整
        if (refConfig.TargetRuneRef is null)
        {
            refConfig.ClearAnalysisCache(); // 清除无效引用的缓存
            return new RuneAnalysisResult
            {
                RuneMessages =
                [
                    new ValidationMessage
                    {
                        Severity = RuleSeverity.Error,
                        Message = "配置无效：尚未选择要引用的全局符文。",
                        RuleSource = "ReferenceValidation"
                    }
                ]
            };
        }

        // Case 2: 异步查找被引用的真实配置
        var findResult = await this.FindService.FindRuneConfigByRefAsync(userId, refConfig.TargetRuneRef);

        if (findResult.TryGetError(out var error, out var storedConfig))
        {
            // Case 3: 找不到引用的配置
            refConfig.ClearAnalysisCache(); // 清除查找失败的缓存
            return new RuneAnalysisResult
            {
                RuneMessages =
                [
                    new ValidationMessage
                    {
                        Severity = RuleSeverity.Error,
                        Message =
                            $"引用解析失败：找不到 RefId='{refConfig.TargetRuneRef.RefId}', Version='{refConfig.TargetRuneRef.Version}' 的全局符文。({error.ToDetailString()})",
                        RuleSource = "ReferenceValidation"
                    }
                ]
            };
        }

        // Case 4: 成功找到，返回真实配置的规格
        var actualConfig = storedConfig.Content;
        var consumed = actualConfig.GetConsumedSpec();
        var produced = actualConfig.GetProducedSpec();
        refConfig.UpdateAnalysisCache(consumed, produced);

        return new RuneAnalysisResult
        {
            ConsumedVariables = consumed,
            ProducedVariables = produced,
            RuneMessages =
            [
                new ValidationMessage
                {
                    Severity = RuleSeverity.Hint, // 使用 Hint 级别来提供参考信息
                    Message = $"引用成功解析：当前显示的是 '{actualConfig.Name}' ({actualConfig.RuneType}) 的输入输出端口。",
                    RuleSource = "ReferenceValidation"
                }
            ]
        };
    }
}