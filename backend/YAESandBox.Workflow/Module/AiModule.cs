using System.ComponentModel;
using System.Runtime.CompilerServices;
using FluentResults;
using YAESandBox.Workflow.AIService;

namespace YAESandBox.Workflow.Module;

/// <summary>
/// Ai调用模块，Ai的配置保存在外部的Step，并且注入到执行函数中，所以这里只需要保存一些临时的调试信息到生成它的<see cref="AiModuleConfig"/>里面。
/// </summary>
/// <param name="onChunkReceivedScript"></param>
internal class AiModule(Action<string> onChunkReceivedScript) : IWorkflowModule
{
    // TODO 这里是回调函数，应该由脚本完成
    private Action<string> onChunkReceivedScript { get; } = onChunkReceivedScript;

    public Task<Result<string>> ExecuteAsync(IAiProcessor aiProcessor, List<RoledPromptDto> prompts, bool isStream)
    {
        switch (isStream)
        {
            case true:
                return this.ExecuteStreamAsync(aiProcessor, prompts);
            case false:
                return this.ExecuteNonStreamAsync(aiProcessor, prompts);
        }
    }

    private async Task<Result<string>> ExecuteStreamAsync(IAiProcessor aiProcessor, List<RoledPromptDto> prompts)
    {
        string fullAiReturn = "";
        var result = await aiProcessor.StreamRequestAsync(prompts, chunk =>
        {
            fullAiReturn += chunk;
            this.onChunkReceivedScript(chunk);
        });
        if (result.IsFailed)
            return result;
        return fullAiReturn;
    }

    private async Task<Result<string>> ExecuteNonStreamAsync(IAiProcessor aiProcessor, List<RoledPromptDto> prompts)
    {
        var result = await aiProcessor.NonStreamRequestAsync(prompts);
        if (result.IsFailed)
            return result;
        this.onChunkReceivedScript(result.Value);
        return result.Value;
    }
}

internal record AiModuleConfig() : AbstractModuleConfig<AiModule>(nameof(AiModuleConfig))
{
    [ReadOnly(true)] public List<RoledPromptDto>? DebugPrompts { get; set; }

    internal override AiModule ToCurrentModule(StepProcessor.StepProcessorContent stepProcessor)
    {
        throw new NotImplementedException();
    }
}