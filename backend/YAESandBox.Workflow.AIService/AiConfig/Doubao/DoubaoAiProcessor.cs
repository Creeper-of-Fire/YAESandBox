// DoubaoAiProcessor.cs

using YAESandBox.Depend.Results;
using YAESandBox.Workflow.AIService.Shared;

namespace YAESandBox.Workflow.AIService.AiConfig.Doubao;

file static class PromptRoleMapper
{
    public static DoubaoChatMessage ToDoubaoMessage(this RoledPromptDto prompt)
    {
        return prompt.Role switch
        {
            PromptRoleType.System => new DoubaoChatMessage("system", prompt.Content),
            PromptRoleType.User => new DoubaoChatMessage("user", prompt.Content, Name: prompt.Name),
            PromptRoleType.Assistant => new DoubaoChatMessage("assistant", prompt.Content),
            _ => throw new ArgumentOutOfRangeException(nameof(prompt.Role), $"不支持的角色: {prompt.Role}")
        };
    }
}

internal class DoubaoAiProcessor(AiProcessorDependencies dependencies, DoubaoAiProcessorConfig parameters) : IAiProcessor
{
    private HttpClient HttpClient { get; } = dependencies.HttpClient;

    private FlexibleAiClient Client { get; } = new(dependencies.HttpClient,
        new ApiClientConfig("https://ark.cn-beijing.volces.com/api/v3/", parameters.ApiKey));

    private DoubaoAiProcessorConfig Config { get; } = parameters;

    public async Task<Result> StreamRequestAsync(
        IEnumerable<RoledPromptDto> prompts,
        StreamRequestCallBack requestCallBack,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestPayload = this.CreateChatRequestPayload(prompts, stream: true);

            await foreach (var chunk in this.Client.StreamChatCompletionsAsync<DoubaoChatRequest, DoubaoStreamChunk>
                               (requestPayload, cancellationToken))
            {
                if (chunk.Choices is not { Count: > 0 }) continue;

                var choice = chunk.Choices[0];

                // 使用通用格式化工具处理响应
                var callBackResult = await AiResponseFormatter.FormatAndInvoke(choice.Delta, requestCallBack);
                if (callBackResult.TryGetError(out var error))
                    return error;
                
                if (!string.IsNullOrEmpty(choice.FinishReason))
                {
                    // 流已结束，可以根据需要处理 'stop', 'length' 等原因
                    break;
                }
            }

            return Result.Ok();
        }
        catch (HttpRequestException ex)
        {
            return AiError.Error("与豆包 API 通信失败。",ex);
        }
        catch (Exception ex)
        {
            return AiError.Error("处理豆包请求时发生未知错误。",ex);
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
                await this.Client.GetChatCompletionsAsync<DoubaoChatRequest, DoubaoCompletionResponse>(requestPayload, cancellationToken);

            if (response.Choices is not { Count: > 0 } || response.Choices[0].Message is null)
            {
                return AiError.Error("豆包 API 返回了无效或空的响应。");
            }

            var message = response.Choices[0].Message;

            // 同样使用通用格式化工具，一次性获取完整内容
            var finalContent = AiResponseFormatter.GetFormattedContent(message);
            
            return await requestCallBack.OnFinalResponseReceivedAsync(finalContent);
        }
        catch (HttpRequestException ex)
        {
            return AiError.Error("与豆包 API 通信失败。",ex);
        }
        catch (Exception ex)
        {
            return AiError.Error("处理豆包请求时发生未知错误。",ex);
        }
    }

    /// <summary>
    /// 根据配置和请求参数，创建一个通用的豆包聊天请求负载。
    /// </summary>
    private DoubaoChatRequest CreateChatRequestPayload(IEnumerable<RoledPromptDto> prompts, bool stream)
    {
        var responseFormat = !string.IsNullOrEmpty(this.Config.ResponseFormatType) && this.Config.ResponseFormatType != "text"
            ? new DoubaoResponseFormat(Type: this.Config.ResponseFormatType)
            : null;

        var streamOptions = stream && this.Config.StreamOptionsIncludeUsage.HasValue
            ? new DoubaoStreamOptions(IncludeUsage: this.Config.StreamOptionsIncludeUsage.Value)
            : null;

        var logitBias = this.Config.LogitBias?.ToDictionary(item => item.TokenId, item => item.BiasValue);

        return new DoubaoChatRequest(
            Model: this.Config.ModelName,
            Messages: prompts.Select(p => p.ToDoubaoMessage()).ToList(),
            Stream: stream,
            Temperature: this.Config.Temperature,
            MaxTokens: this.Config.MaxOutputTokens,
            TopP: this.Config.TopP,
            Stop: this.Config.StopSequences,
            ResponseFormat: responseFormat,
            FrequencyPenalty: this.Config.FrequencyPenalty,
            PresencePenalty: this.Config.PresencePenalty,
            StreamOptions: streamOptions,
            ServiceTier: this.Config.ServiceTier,
            Logprobs: this.Config.Logprobs,
            TopLogprobs: this.Config.TopLogprobs,
            LogitBias: logitBias
        );
    }
}