using System.ComponentModel;
using FluentResults;
using YAESandBox.Workflow.AIService;
using YAESandBox.Workflow.DebugDto;
using static YAESandBox.Workflow.Step.StepProcessor;

namespace YAESandBox.Workflow.Module;

/// <summary>
/// Ai调用模块，Ai的配置保存在外部的Step，并且注入到执行函数中，所以这里只需要保存一些临时的调试信息到生成它的<see cref="AiModuleConfig"/>里面。
/// </summary>
/// <param name="onChunkReceivedScript"></param>
internal class AiModuleProcessor(Action<string> onChunkReceivedScript) : IModuleProcessor
{
    /// <inheritdoc />
    public IModuleProcessorDebugDto DebugDto { get; } = new AiModuleProcessorDebugDto();

    internal class AiModuleProcessorDebugDto : IModuleProcessorDebugDto
    {
        public IList<RoledPromptDto> Prompts { get; init; } = [];
        public int TokenUsage { get; set; } = 0;
    }

    // TODO 这里是回调函数，应该由脚本完成
    private Action<string> OnChunkReceivedScript { get; } = onChunkReceivedScript;

    public Task<Result<string>> ExecuteAsync(IAiProcessor aiProcessor, IEnumerable<RoledPromptDto> prompts, bool isStream)
    {
        return isStream switch
        {
            true => this.ExecuteStreamAsync(aiProcessor, prompts),
            false => this.ExecuteNonStreamAsync(aiProcessor, prompts)
        };
    }

    private async Task<Result<string>> ExecuteStreamAsync(IAiProcessor aiProcessor, IEnumerable<RoledPromptDto> prompts)
    {
        string fullAiReturn = "";
        var result = await aiProcessor.StreamRequestAsync(prompts, new StreamRequestCallBack
        {
            OnChunkReceived = chunk =>
            {
                fullAiReturn += chunk;
                this.OnChunkReceivedScript(chunk);
            }
        });
        if (result.IsFailed)
            return result;
        return fullAiReturn;
    }

    private async Task<Result<string>> ExecuteNonStreamAsync(IAiProcessor aiProcessor, IEnumerable<RoledPromptDto> prompts)
    {
        var result = await aiProcessor.NonStreamRequestAsync(prompts);
        if (!result.TryGetValue(out string? value))
            return result;
        this.OnChunkReceivedScript(value);
        return value;
    }
}

internal record AiModuleConfig() : AbstractModuleConfig<AiModuleProcessor>(nameof(AiModuleConfig))
{
    protected override AiModuleProcessor ToCurrentModule(StepProcessorContent stepProcessor)
    {
        throw new NotImplementedException();
    }
}