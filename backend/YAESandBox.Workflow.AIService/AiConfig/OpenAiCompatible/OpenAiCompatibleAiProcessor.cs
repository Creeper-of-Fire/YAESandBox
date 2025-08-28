using YAESandBox.Depend.Results;
using YAESandBox.Workflow.AIService.Shared;

namespace YAESandBox.Workflow.AIService.AiConfig.OpenAiCompatible;

file static class PromptRoleMapper
{
    public static OpenAiChatMessage ToOpenAiMessage(this RoledPromptDto prompt)
    {
        return prompt.Role switch
        {
            PromptRoleType.System => new OpenAiChatMessage("system", prompt.Content),
            PromptRoleType.User => new OpenAiChatMessage("user", prompt.Content, Name: prompt.Name),
            PromptRoleType.Assistant => new OpenAiChatMessage("assistant", prompt.Content),
            _ => throw new ArgumentOutOfRangeException(nameof(prompt.Role), $"不支持的角色: {prompt.Role}")
        };
    }
}

internal class OpenAiCompatibleAiProcessor(AiProcessorDependencies dependencies, OpenAiCompatibleAiProcessorConfig parameters) : IAiProcessor
{
    // 关键改动：使用配置中的 BaseUrl 来初始化客户端
    private FlexibleAiClient Client { get; } = new(dependencies.HttpClient,
        new ApiClientConfig(parameters.BaseUrl, parameters.ApiKey));

    private OpenAiCompatibleAiProcessorConfig Config { get; } = parameters;

    public async Task<Result> StreamRequestAsync(
        IEnumerable<RoledPromptDto> prompts,
        StreamRequestCallBack requestCallBack,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestPayload = this.CreateChatRequestPayload(prompts, stream: true);

            // 使用新的 OpenAi 模型类型
            await foreach (var chunk in this.Client.StreamChatCompletionsAsync<OpenAiChatRequest, OpenAiStreamChunk>
                               (requestPayload, cancellationToken))
            {
                if (chunk.Choices is not { Count: > 0 }) continue;

                var choice = chunk.Choices[0];

                var callBackResult = await AiResponseFormatter.FormatAndInvoke(choice.Delta, requestCallBack);
                if (callBackResult.TryGetError(out var error))
                    return error;
                
                if (!string.IsNullOrEmpty(choice.FinishReason))
                {
                    break;
                }
            }

            return Result.Ok();
        }
        catch (HttpRequestException ex)
        {
            return AiError.Error($"与通用 OpenAI API ({this.Config.BaseUrl}) 通信失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            return AiError.Error($"处理通用 OpenAI 请求时发生未知错误: {ex.Message}");
        }
    }
    
    public async Task<Result> NonStreamRequestAsync(
        IEnumerable<RoledPromptDto> prompts,
        NonStreamRequestCallBack requestCallBack,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestPayload = this.CreateChatRequestPayload(prompts, stream: false);

            var response =
                await this.Client.GetChatCompletionsAsync<OpenAiChatRequest, OpenAiCompletionResponse>(requestPayload, cancellationToken);

            if (response.Choices is not { Count: > 0 })
            {
                return AiError.Error("通用 OpenAI API 返回了无效或空的响应。");
            }

            var message = response.Choices[0].Message;

            var finalContent = AiResponseFormatter.GetFormattedContent(message);
            
            return await requestCallBack.OnFinalResponseReceivedAsync(finalContent);
        }
        catch (HttpRequestException ex)
        {
            return AiError.Error($"与通用 OpenAI API ({this.Config.BaseUrl}) 通信失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            return AiError.Error($"处理通用 OpenAI 请求时发生未知错误: {ex.Message}");
        }
    }

    private OpenAiChatRequest CreateChatRequestPayload(IEnumerable<RoledPromptDto> prompts, bool stream)
    {
        var responseFormat = !string.IsNullOrEmpty(this.Config.ResponseFormatType) && this.Config.ResponseFormatType != "text"
            ? new OpenAiResponseFormat(Type: this.Config.ResponseFormatType)
            : null;
        
        return new OpenAiChatRequest(
            Model: this.Config.ModelName,
            Messages: prompts.Select(p => p.ToOpenAiMessage()).ToList(),
            Stream: stream,
            Temperature: this.Config.Temperature,
            MaxTokens: this.Config.MaxOutputTokens,
            TopP: this.Config.TopP,
            ResponseFormat: responseFormat
        );
    }
}