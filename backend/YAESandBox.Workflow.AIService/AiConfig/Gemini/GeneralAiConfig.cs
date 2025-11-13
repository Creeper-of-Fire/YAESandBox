using YAESandBox.Depend.Results;
using YAESandBox.Workflow.AIService.Shared;

namespace YAESandBox.Workflow.AIService.AiConfig.Gemini;

file static class PromptMapper
{
    public static (GeminiContent? systemInstruction, List<GeminiContent> userMessages) ToGeminiContents(
        this IEnumerable<RoledPromptDto> prompts)
    {
        GeminiContent? systemInstruction = null;
        var userMessages = new List<GeminiContent>();
        string? lastRole = null;

        foreach (var prompt in prompts)
        {
            if (prompt.Role == PromptRoleType.System)
            {
                // Gemini 的 system prompt 是独立的
                systemInstruction = new GeminiContent { Parts = [new GeminiPart { Text = prompt.Content }] };
                continue;
            }

            string currentRole = prompt.Role switch
            {
                PromptRoleType.User => "user",
                PromptRoleType.Assistant => "model",
                _ => throw new ArgumentOutOfRangeException(nameof(prompt.Role), $"不支持的角色: {prompt.Role}")
            };

            // Gemini 要求 user 和 model 角色交替出现。如果连续出现相同角色，需要合并。
            if (userMessages.Count > 0 && lastRole == currentRole)
            {
                var lastMessage = userMessages[^1];
                var lastPart = lastMessage.Parts?.LastOrDefault();
                if (lastPart is not null)
                {
                    // 将内容追加到最后一个 part
                    userMessages[^1] = lastMessage with
                    {
                        Parts = [lastPart with { Text = lastPart.Text + "\n" + prompt.Content }]
                    };
                }
            }
            else
            {
                userMessages.Add(new GeminiContent
                {
                    Role = currentRole,
                    Parts = [new GeminiPart { Text = prompt.Content }]
                });
            }

            lastRole = currentRole;
        }

        return (systemInstruction, userMessages);
    }
}

internal class GeminiAiProcessor(AiProcessorDependencies dependencies, GeminiAiProcessorConfig parameters) : IAiProcessor
{
    private GeminiApiClient Client { get; } = new(dependencies.HttpClient,
        new ApiClientConfig("https://generativelanguage.googleapis.com/v1beta/", parameters.ApiKey));

    private GeminiAiProcessorConfig Config { get; } = parameters;

    public async Task<Result> StreamRequestAsync(
        IEnumerable<RoledPromptDto> prompts,
        StreamRequestCallBack requestCallBack,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestPayload = this.CreateGenerateContentRequestPayload(prompts);
            string modelName = this.Config.ModelName ?? throw new InvalidOperationException("Gemini模型名称未配置。");

            await foreach (var chunk in this.Client.StreamChatCompletionsAsync<GeminiGenerateContentRequest, GeminiGenerateContentResponse>
                               (requestPayload, modelName, cancellationToken))
            {
                if (chunk.Candidates is not { Count: > 0 }) continue;

                // 使用通用格式化工具处理响应
                var callBackResult = await AiResponseFormatter.FormatAndInvoke(chunk, requestCallBack);
                if (callBackResult.TryGetError(out var error))
                    return error;

                // 检查结束原因
                if (!string.IsNullOrEmpty(chunk.Candidates[0].FinishReason))
                {
                    break;
                }
            }

            return Result.Ok();
        }
        catch (HttpRequestException ex)
        {
            return AiError.Error("与 Gemini API 通信失败。", ex);
        }
        catch (Exception ex)
        {
            return AiError.Error("处理 Gemini 请求时发生未知错误。", ex);
        }
    }

    public async Task<Result> NonStreamRequestAsync(
        IEnumerable<RoledPromptDto> prompts,
        NonStreamRequestCallBack requestCallBack,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestPayload = this.CreateGenerateContentRequestPayload(prompts);
            string modelName = this.Config.ModelName ?? throw new InvalidOperationException("Gemini模型名称未配置。");

            var response =
                await this.Client.GetChatCompletionsAsync<GeminiGenerateContentRequest, GeminiGenerateContentResponse>(requestPayload,
                    modelName, cancellationToken);

            if (response.Candidates is not { Count: > 0 } || response.Candidates[0].Content?.Parts is null)
            {
                // 可以增加对 response.PromptFeedback 的检查来提供更详细的错误信息
                return AiError.Error("Gemini API 返回了无效或空的响应。");
            }

            // 同样使用通用格式化工具，一次性获取完整内容
            var finalContent = AiResponseFormatter.GetFormattedContent(response);

            return await requestCallBack.OnFinalResponseReceivedAsync(finalContent);
        }
        catch (HttpRequestException ex)
        {
            return AiError.Error($"与 Gemini API 通信失败。", ex);
        }
        catch (Exception ex)
        {
            return AiError.Error($"处理 Gemini 请求时发生未知错误。", ex);
        }
    }

    private GeminiGenerateContentRequest CreateGenerateContentRequestPayload(IEnumerable<RoledPromptDto> prompts)
    {
        var (systemInstruction, userMessages) = prompts.ToGeminiContents();

        var generationConfig = new GeminiGenerationConfig
        {
            Temperature = this.Config.Temperature,
            TopP = this.Config.TopP,
            TopK = this.Config.TopK,
            MaxOutputTokens = this.Config.MaxOutputTokens,
            StopSequences = this.Config.StopSequences,
            ResponseMimeType = this.Config.ResponseMimeType
        };

        return new GeminiGenerateContentRequest
        {
            SystemInstruction = systemInstruction,
            Contents = userMessages,
            GenerationConfig = generationConfig
        };
    }
}