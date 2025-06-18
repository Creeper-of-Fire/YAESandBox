using YAESandBox.Depend.Results;
using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.DebugDto;
using static YAESandBox.Workflow.Module.ExactModule.TemporaryShowAiReturnProcessor;
using static YAESandBox.Workflow.Step.StepProcessor;

namespace YAESandBox.Workflow.Module.ExactModule;

/// <summary>
/// 【临时模块】
/// </summary>
/// <param name="workflowRuntimeService"><see cref="WorkflowRuntimeService"/></param>
/// <param name="config">模块配置。</param>
internal class TemporaryShowAiReturnProcessor(
    WorkflowRuntimeService workflowRuntimeService,
    TemporaryShowAiReturnConfig config)
    : IWithDebugDto<TemporaryShowAiReturnProcessorDebugDto>, INormalModule
{
    private WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;
    private TemporaryShowAiReturnConfig Config { get; } = config;

    public TemporaryShowAiReturnProcessorDebugDto DebugDto => new();

    public record TemporaryShowAiReturnProcessorDebugDto : IModuleProcessorDebugDto;

    public async Task<Result> ExecuteAsync(StepProcessorContent stepProcessorContent, CancellationToken cancellationToken = default)
    {
        if (stepProcessorContent.FullAiReturn == null)
            return Result.Ok(); // 只处理非null的输出

        string aiOutput = "新AI的生成：" + stepProcessorContent.FullAiReturn + "\n";

        return await this.WorkflowRuntimeService.CallbackAsync<IWorkflowCallbackDisplayUpdate>(it => it.DisplayUpdateAsync(aiOutput));
    }
}

/// <summary>
/// 【临时模块】
/// </summary>
internal record TemporaryShowAiReturnConfig : AbstractModuleConfig<TemporaryShowAiReturnProcessor>
{
    /// <inheritdoc />
    protected override TemporaryShowAiReturnProcessor ToCurrentModule(WorkflowRuntimeService workflowRuntimeService) =>
        new(workflowRuntimeService, this);
}