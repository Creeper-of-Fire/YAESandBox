using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.SchemaProcessor;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Core;
using YAESandBox.Workflow.Core.Abstractions;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.VarSpec;
using static YAESandBox.Workflow.Rune.ExactRune.SendToRawTextRuneProcessor;
using static YAESandBox.Workflow.Tuum.TuumProcessor;

namespace YAESandBox.Workflow.Rune.ExactRune;

/// <summary>
/// 用于将祝祷变量名直接写入到 WorkflowRuntimeService.RawText。
/// </summary>
/// <param name="workflowRuntimeService"><see cref="WorkflowRuntimeService"/></param>
/// <param name="config">符文配置。</param>
internal class SendToRawTextRuneProcessor(
    WorkflowRuntimeService workflowRuntimeService,
    SendToRawTextRuneConfig config)
    : IProcessorWithDebugDto<SendToRawTextRuneProcessorDebugDto>, INormalRune
{
    private WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;
    private SendToRawTextRuneConfig Config { get; } = config;

    // 这个临时符文非常简单，可能不需要复杂的Debug DTO，
    // 但为了接口一致性，可以提供一个最小化的实现或直接返回 null。
    // 为了简单起见，这里我们先不实现具体的Debug DTO。
    public SendToRawTextRuneProcessorDebugDto DebugDto => new(); // 暂时不提供Debug信息

    public record SendToRawTextRuneProcessorDebugDto : IRuneProcessorDebugDto;

    public async Task<Result> ExecuteAsync(TuumProcessorContent tuumProcessorContent, CancellationToken cancellationToken = default)
    {
        string? outputVar = tuumProcessorContent.GetTuumVar(this.Config.RequireVariables)?.ToString();
        if (outputVar == null)
            return Result.Ok(); // 只处理非null的输出

        return await this.WorkflowRuntimeService.CallbackAsync<IWorkflowCallbackSendFinalRawText>(it =>
            it.SendFinalRawTextAsync(outputVar));
    }
}

/// <summary>
/// 用于将祝祷变量名直接写入到 WorkflowRuntimeService.RawText 的配置。
/// 该配置定义了需要从祝祷中提取并存储到RawText中的变量。
/// </summary>
[InLastTuum]
[ClassLabel("😼结束")]
internal record SendToRawTextRuneConfig : AbstractRuneConfig<SendToRawTextRuneProcessor>
{
    /// <inheritdoc />
    protected override SendToRawTextRuneProcessor ToCurrentRune(WorkflowRuntimeService workflowRuntimeService) =>
        new(workflowRuntimeService, this);

    /// <summary>
    /// 获取执行此符文所需的变量名
    /// </summary>
    [Required]
    [Display(
        Name = "需求变量名",
        Description = "指定需要从祝祷中提取并写入RawText的变量名称",
        Prompt = "请输入变量名"
    )]
    public required string RequireVariables { get; init; } = "";

    /// <inheritdoc />
    public override List<ConsumedSpec> GetConsumedSpec() => [new(this.RequireVariables, CoreVarDefs.String)];
}