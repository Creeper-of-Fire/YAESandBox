using FluentResults;
using YAESandBox.Workflow.AIService;

namespace YAESandBox.Workflow;

public abstract record ModuleConfig : IModuleConfig
{
    public abstract IWorkflowModule ToModule(StepProcessor.StepProcessorContent stepProcessor);
}

public interface IModuleConfig
{
    IWorkflowModule ToModule(StepProcessor.StepProcessorContent stepProcessor);
}

public class AiModule : IWorkflowModule
{
    // TODO 这里是回调函数，应该由脚本完成
    private Action<string> onChunkReceivedScript { get; }

    public Task<Result<string>> ExecuteAsync(IAiProcessor aiProcessor, List<(PromptRole role, string prompt)> prompts, bool isStream)
    {
        switch (isStream)
        {
            case true:
                return ExecuteStreamAsync(aiProcessor, prompts);
            case false:
                return ExecuteNonStreamAsync(aiProcessor, prompts);
        }
    }

    private async Task<Result<string>> ExecuteStreamAsync(IAiProcessor aiProcessor, List<(PromptRole role, string prompt)> prompts)
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
    private async Task<Result<string>> ExecuteNonStreamAsync(IAiProcessor aiProcessor, List<(PromptRole role, string prompt)> prompts)
    {
        // var result = await aiProcessor.RequestAsync(prompts);
        // if (result.IsFailed)
        //     return result;
        // return result.Value.Content;
        // TODO
        throw new NotImplementedException();
    }
}

public interface IWorkflowModule
{
    // Task ExecuteAsync(dynamic input,dynamic output,);
}