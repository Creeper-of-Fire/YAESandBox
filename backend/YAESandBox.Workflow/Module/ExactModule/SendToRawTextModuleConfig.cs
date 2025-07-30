using System.ComponentModel.DataAnnotations;
using YAESandBox.Depend.Results;
using YAESandBox.Depend.Schema.Attributes;
using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.API.Schema;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.DebugDto;
using static YAESandBox.Workflow.Module.ExactModule.SendToRawTextModuleProcessor;
using static YAESandBox.Workflow.Step.StepProcessor;

namespace YAESandBox.Workflow.Module.ExactModule;

/// <summary>
/// 用于将步骤变量名直接写入到 WorkflowRuntimeService.RawText。
/// </summary>
/// <param name="workflowRuntimeService"><see cref="WorkflowRuntimeService"/></param>
/// <param name="config">模块配置。</param>
internal class SendToRawTextModuleProcessor(
    WorkflowRuntimeService workflowRuntimeService,
    SendToRawTextModuleConfig config)
    : IWithDebugDto<SendToRawTextModuleProcessorDebugDto>, INormalModule
{
    private WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;
    private SendToRawTextModuleConfig Config { get; } = config;

    // 这个临时模块非常简单，可能不需要复杂的Debug DTO，
    // 但为了接口一致性，可以提供一个最小化的实现或直接返回 null。
    // 为了简单起见，这里我们先不实现具体的Debug DTO。
    public SendToRawTextModuleProcessorDebugDto DebugDto => new(); // 暂时不提供Debug信息

    public record SendToRawTextModuleProcessorDebugDto : IModuleProcessorDebugDto;

    public async Task<Result> ExecuteAsync(StepProcessorContent stepProcessorContent, CancellationToken cancellationToken = default)
    {
        string? outputVar = stepProcessorContent.InputVar(this.Config.RequireVariables)?.ToString();
        if (outputVar == null)
            return Result.Ok(); // 只处理非null的输出

        return await this.WorkflowRuntimeService.CallbackAsync<IWorkflowCallbackSendFinalRawText>(it =>
            it.SendFinalRawTextAsync(outputVar));
    }
}

/// <summary>
/// 用于将步骤变量名直接写入到 WorkflowRuntimeService.RawText 的配置。
/// 该配置定义了需要从步骤中提取并存储到RawText中的变量。
/// </summary>
[InLastStep]
[ClassLabel("😼结束")]
internal record SendToRawTextModuleConfig : AbstractModuleConfig<SendToRawTextModuleProcessor>
{
    /// <inheritdoc />
    protected override SendToRawTextModuleProcessor ToCurrentModule(WorkflowRuntimeService workflowRuntimeService) =>
        new(workflowRuntimeService, this);

    /// <summary>
    /// 获取执行此模块所需的变量名
    /// </summary>
    [Required]
    [Display(
        Name = "需求变量名",
        Description = "指定需要从步骤中提取并写入RawText的变量名称",
        Prompt = "请输入变量名"
    )]
    public required string RequireVariables { get; init; } = "";

    /// <inheritdoc />
    internal override List<string> GetConsumedVariables() => [this.RequireVariables];
}