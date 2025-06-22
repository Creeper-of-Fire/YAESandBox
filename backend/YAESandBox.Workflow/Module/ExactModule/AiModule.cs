using System.ComponentModel;
using YAESandBox.Depend.Results;
using YAESandBox.Workflow.Abstractions;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.Config;
using YAESandBox.Workflow.DebugDto;
using YAESandBox.Workflow.Module.ModuleAttribute;
using static YAESandBox.Workflow.Module.ExactModule.AiModuleProcessor;

namespace YAESandBox.Workflow.Module.ExactModule;

/// <summary>
/// Ai调用模块，Ai的配置保存在外部的Step，并且注入到执行函数中，所以这里只需要保存一些临时的调试信息到生成它的<see cref="AiModuleConfig"/>里面。
/// </summary>
/// <param name="onChunkReceivedScript"></param>
internal class AiModuleProcessor(Action<string> onChunkReceivedScript)
    : IWithDebugDto<AiModuleProcessorDebugDto>
{
    /// <inheritdoc />
    public AiModuleProcessorDebugDto DebugDto { get; } = new();

    internal class AiModuleProcessorDebugDto : IModuleProcessorDebugDto
    {
        public IList<RoledPromptDto> Prompts { get; init; } = [];
        public int TokenUsage { get; set; } = 0;
    }

    // TODO 这里是回调函数，应该由脚本完成
    private Action<string> OnChunkReceivedScript { get; } = onChunkReceivedScript;

    /// <summary>
    /// AI模块的运行
    /// </summary>
    /// <param name="aiProcessor">从AI配置中实例化的运行时对象</param>
    /// <param name="prompts">提示词</param>
    /// <param name="isStream">是否流式</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>返回AI最终组装完成的输出</returns>
    public Task<Result<string>> ExecuteAsync
        (IAiProcessor aiProcessor, IEnumerable<RoledPromptDto> prompts, bool isStream, CancellationToken cancellationToken = default)
    {
        return isStream switch
        {
            true => this.ExecuteStreamAsync(aiProcessor, prompts, cancellationToken),
            false => this.ExecuteNonStreamAsync(aiProcessor, prompts, cancellationToken)
        };
    }

    private async Task<Result<string>> ExecuteStreamAsync
        (IAiProcessor aiProcessor, IEnumerable<RoledPromptDto> prompts, CancellationToken cancellationToken = default)
    {
        string fullAiReturn = "";
        var result = await aiProcessor.StreamRequestAsync(prompts, new StreamRequestCallBack
        {
            OnChunkReceived = chunk =>
            {
                fullAiReturn += chunk;
                this.OnChunkReceivedScript(chunk);
            }
        }, cancellationToken);
        if (result.TryGetError(out var error))
            return error;
        return fullAiReturn;
    }

    private async Task<Result<string>> ExecuteNonStreamAsync
        (IAiProcessor aiProcessor, IEnumerable<RoledPromptDto> prompts, CancellationToken cancellationToken = default)
    {
        var result = await aiProcessor.NonStreamRequestAsync(prompts, cancellationToken: cancellationToken);
        if (!result.TryGetValue(out string? value))
            return result;
        this.OnChunkReceivedScript(value);
        return value;
    }
}

[NoConfig]
[SingleInStep]
[Behind(typeof(PromptGenerationModuleConfig))]
internal record AiModuleConfig : AbstractModuleConfig<AiModuleProcessor>
{
    /// <inheritdoc />
    [DefaultValue("AI模块")]
    [ReadOnly(true)]
    public override string Name { get; init; } = string.Empty;

    protected override AiModuleProcessor ToCurrentModule(WorkflowRuntimeService workflowRuntimeService) =>
        new(s => { _ = workflowRuntimeService.Callback<IWorkflowCallbackDisplayUpdate>(it => it.DisplayUpdateAsync(s)); });
}