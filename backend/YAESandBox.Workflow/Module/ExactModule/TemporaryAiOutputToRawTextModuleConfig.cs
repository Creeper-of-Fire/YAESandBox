using YAESandBox.Depend.Results;
using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.DebugDto;
using static YAESandBox.Workflow.Module.ExactModule.TemporaryAiOutputToRawTextModuleProcessor;
using static YAESandBox.Workflow.Step.StepProcessor;

namespace YAESandBox.Workflow.Module.ExactModule;

/// <summary>
/// 【临时模块】处理器，用于将 AI 模块的输出 (StepProcessorContent.FullAiReturn)
/// 直接写入到 WorkflowRuntimeService.RawText。
/// 这个模块是临时的，未来会被更完善的变量传递和文本组装机制取代。
/// </summary>
/// <param name="workflowRuntimeService"><see cref="WorkflowRuntimeService"/></param>
/// <param name="config">模块配置。</param>
internal class TemporaryAiOutputToRawTextModuleProcessor(
    WorkflowRuntimeService workflowRuntimeService,
    TemporaryAiOutputToRawTextModuleConfig config)
    : IWithDebugDto<TemporaryAiOutputToRawTextModuleProcessorDebugDto>, INormalModule
{
    private WorkflowRuntimeService WorkflowRuntimeService { get; } = workflowRuntimeService;
    private TemporaryAiOutputToRawTextModuleConfig Config { get; } = config;

    // 这个临时模块非常简单，可能不需要复杂的Debug DTO，
    // 但为了接口一致性，可以提供一个最小化的实现或直接返回 null。
    // 为了简单起见，这里我们先不实现具体的Debug DTO。
    public TemporaryAiOutputToRawTextModuleProcessorDebugDto DebugDto => new(); // 暂时不提供Debug信息

    public record TemporaryAiOutputToRawTextModuleProcessorDebugDto : IModuleProcessorDebugDto;

    public async Task<Result> ExecuteAsync(StepProcessorContent stepProcessorContent, CancellationToken cancellationToken = default)
    {
        // 从 StepProcessorContent.FullAiReturn 获取AI的输出
        // 这是 AiModuleProcessor 放置其结果的固定位置
        if (stepProcessorContent.FullAiReturn == null)
            return Result.Ok(); // 只处理非null的输出

        string aiOutput = "最终得到的AI生成：" + stepProcessorContent.FullAiReturn + "\n";

        return await this.WorkflowRuntimeService.CallbackAsync<IWorkflowCallbackSendFinalRawText>(it => it.SendFinalRawTextAsync(aiOutput));
    }
}

/// <summary>
/// 【临时模块】用于将AI模块的输出 (StepProcessorContent.FullAiReturn) 直接写入到 WorkflowRuntimeService.RawText 的配置。
/// 这个模块是临时的，未来会被更完善的变量传递和文本组装机制取代。
/// </summary>
internal record TemporaryAiOutputToRawTextModuleConfig : AbstractModuleConfig<TemporaryAiOutputToRawTextModuleProcessor>
{
    /// <inheritdoc />
    protected override TemporaryAiOutputToRawTextModuleProcessor ToCurrentModule(WorkflowRuntimeService workflowRuntimeService) =>
        new(workflowRuntimeService, this);
}