// 文件路径: YAESandBox.Workflow.AIService/AiConfig/DeepSeek/DeepSeekAiProcessor.cs

using YAESandBox.Depend.Results;
using YAESandBox.Workflow.AIService.Shared;

namespace YAESandBox.Workflow.AIService.AiConfig.DeepSeek;

/// <summary>
/// 提供从通用 RoledPromptDto 到 DeepSeek 特定消息格式的映射。
/// 将映射逻辑分离出来，保持 Processor 的纯粹性。
/// </summary>
file static class DeepSeekPromptMapper
{
    public static DeepSeekChatMessage ToDeepSeekMessage(this RoledPromptDto prompt)
    {
        string role = prompt.Type switch
        {
            PromptRoleType.System => "system",
            PromptRoleType.User => "user",
            PromptRoleType.Assistant => "assistant",
            _ => throw new ArgumentOutOfRangeException(nameof(prompt.Type), $"不支持的角色类型: {prompt.Type}")
        };
        return new DeepSeekChatMessage(role, prompt.Content, prompt.Name);
    }
}

internal class DeepSeekAiProcessor(AiProcessorDependencies dependencies, DeepSeekAiProcessorConfig config) : IAiProcessor
{
    private DeepSeekAiProcessorConfig Config { get; } = config;

    private FlexibleOpenAiClient Client { get; } = new(dependencies.HttpClient, new ApiClientConfig(
        BaseUrl: "https://api.deepseek.com",
        ApiKey: config.ApiKey
    ));

    public async Task<Result> StreamRequestAsync(
        IEnumerable<RoledPromptDto> prompts,
        StreamRequestCallBack requestCallBack,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestPayload = this.CreateChatRequestPayload(prompts, stream: true);

            await foreach (var chunk in this.Client.StreamChatCompletionsAsync<DeepSeekChatRequest, DeepSeekStreamChunk>
                               (requestPayload, cancellationToken))
            {
                if (chunk.Choices is not { Count: > 0 }) continue;

                var choice = chunk.Choices[0];

                AiResponseFormatter.FormatAndInvoke(choice.Delta, requestCallBack);

                if (!string.IsNullOrEmpty(choice.FinishReason))
                {
                    break;
                }
            }

            return Result.Ok();
        }
        catch (HttpRequestException ex)
        {
            return AiError.Error($"与 DeepSeek API 通信失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            return AiError.Error($"处理 DeepSeek 请求时发生未知错误: {ex.Message}");
        }
    }

    public async Task<Result<string>> NonStreamRequestAsync(
        IEnumerable<RoledPromptDto> prompts,
        NonStreamRequestCallBack? requestCallBack,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestPayload = this.CreateChatRequestPayload(prompts, stream: false);

            var response =
                await this.Client.GetChatCompletionsAsync<DeepSeekChatRequest, DeepSeekCompletionResponse>(requestPayload,
                    cancellationToken);

            if (response.Choices is not { Count: > 0 } || response.Choices[0].Message is null)
            {
                return AiError.Error("DeepSeek API 返回了无效或空的响应。");
            }

            var message = response.Choices[0].Message;
            string finalContent = AiResponseFormatter.GetFormattedContent(message);

            return Result.Ok(finalContent);
        }
        catch (HttpRequestException ex)
        {
            return AiError.Error($"与 DeepSeek API 通信失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            return AiError.Error($"处理 DeepSeek 请求时发生未知错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 创建一个通用的请求负载，用于流式和非流式请求。
    /// </summary>
    private DeepSeekChatRequest CreateChatRequestPayload(IEnumerable<RoledPromptDto> prompts, bool stream)
    {
        // 构造 response_format 对象
        var responseFormat = !string.IsNullOrEmpty(this.Config.ResponseFormatType) && this.Config.ResponseFormatType != "text"
            ? new { type = this.Config.ResponseFormatType }
            : null;

        // 构造 stream_options 对象
        var streamOptions = stream && this.Config.StreamOptionsIncludeUsage.HasValue
            ? new { include_usage = this.Config.StreamOptionsIncludeUsage.Value }
            : null;

        return new DeepSeekChatRequest(
            Model: this.Config.ModelName,
            Messages: prompts.Select(p => p.ToDeepSeekMessage()).ToList(),
            Stream: stream,
            Temperature: this.Config.Temperature,
            MaxTokens: this.Config.MaxOutputTokens,
            TopP: this.Config.TopP,
            Stop: this.Config.StopSequences,
            ResponseFormat: responseFormat,
            FrequencyPenalty: this.Config.FrequencyPenalty,
            PresencePenalty: this.Config.PresencePenalty,
            StreamOptions: streamOptions,
            Logprobs: this.Config.Logprobs,
            TopLogprobs: this.Config.TopLogprobs
        );
    }
}