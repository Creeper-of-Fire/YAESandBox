// DoubaoAiProcessor.cs

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentResults;

namespace YAESandBox.Workflow.AIService.AiConfig.Doubao;

file static class PromptRoleMapper
{
    // public static string ToDoubaoRoleString(this PromptRole role) => role.type switch
    // {
    //     PromptRoleType.System => "system",
    //     PromptRoleType.User => "user",
    //     PromptRoleType.Assistant => "assistant",
    //     _ => throw new ArgumentOutOfRangeException(nameof(role), $"不支持的角色: {role}") // 已改为中文
    // };

    public static DoubaoChatMessage ToDoubaoMessage(this RoledPromptDto prompt)
    {
        return prompt.Type switch
        {
            PromptRoleType.System => new DoubaoChatMessage("system", prompt.Content),
            PromptRoleType.User => new DoubaoChatMessage("user", prompt.Content, Name: prompt.Name),
            PromptRoleType.Assistant => new DoubaoChatMessage("assistant", prompt.Content),
            _ => throw new ArgumentOutOfRangeException(nameof(prompt.Type), $"不支持的角色: {prompt.Type}")
        };
    }
}

// TODO 目前是手写通讯，以后需要一个通用的通讯机制
internal class DoubaoAiProcessor(AiProcessorDependencies dependencies, DoubaoAiProcessorConfig parameters) : IAiProcessor
{
    private HttpClient HttpClient { get; } = dependencies.HttpClient;
    private DoubaoAiProcessorConfig Config { get; } = parameters;

    public async Task<Result> StreamRequestAsync(
        IEnumerable<RoledPromptDto> prompts,
        StreamRequestCallBack requestCallBack,
        CancellationToken cancellationToken = default)
    {
        List<DoubaoChatMessage> doubaoMessages = [.. prompts.Select(p => p.ToDoubaoMessage())];

        var responseFormatParam = new DoubaoResponseFormat(this.Config.ResponseFormatType);

        var requestPayload = new DoubaoChatRequest(
            Model: this.Config.ModelName,
            Messages: doubaoMessages,
            Stream: true,
            Temperature: this.Config.Temperature,
            MaxTokens: this.Config.MaxOutputTokens,
            TopP: this.Config.TopP,
            Stop: this.Config.StopSequences,
            ResponseFormat: responseFormatParam,
            FrequencyPenalty: this.Config.FrequencyPenalty,
            PresencePenalty: this.Config.PresencePenalty,
            StreamOptions: new DoubaoStreamOptions(this.Config.StreamOptionsIncludeUsage),
            ServiceTier: this.Config.ServiceTier,
            Logprobs: this.Config.Logprobs,
            TopLogprobs: this.Config.TopLogprobs,
            LogitBias: this.Config.LogitBias?.ToDictionary(kvp => kvp.TokenId, kvp => kvp.BiasValue),
            Tools: null
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "https://ark.cn-beijing.volces.com/api/v3/chat/completions")
        {
            Content = JsonContent.Create(requestPayload,
                mediaType: new MediaTypeHeaderValue("application/json"),
                options: new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.Config.ApiKey); // 从 _config 获取 ApiKey

        string? lastFinishReason = null;

        try
        {
            using var response =
                await this.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                // 豆包似乎不会返回错误JSON结构，而是单纯的返回错误响应
                return AiError.Error(
                    $"豆包 API 请求失败: {response.StatusCode}. 响应: {errorContent[..Math.Min(500, errorContent.Length)]}");
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var streamDisposable = stream;
            using var reader = new StreamReader(stream);

            const string sseDataPrefix = "data:";

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AiError.Error("用户取消了 AI 请求。");

                if (string.IsNullOrWhiteSpace(line)) // 跳过SSE中的空行 (通常用于保持连接)
                    continue;

                string jsonData;
                if (line.StartsWith(sseDataPrefix, StringComparison.Ordinal))
                {
                    jsonData = line[sseDataPrefix.Length..].Trim(); // 移除 "data:" 前缀并去除首尾空格

                    if (string.IsNullOrWhiteSpace(jsonData)) // 如果 "data:" 后面是空的或只有空格
                        continue;

                    if (jsonData == "[DONE]") // **关键修正: 处理 "data: [DONE]"**
                    {
                        lastFinishReason = "stop"; // 标记流正常结束
                        break; // 结束读取循环
                    }
                }
                else if (line == "[DONE]") // 处理非标准SSE，但某些API可能直接发送 "[DONE]"
                {
                    lastFinishReason = "stop"; // 标记流正常结束
                    break; // 结束读取循环
                }
                else
                {
                    continue; // 如果行既不以 "data:" 开头，也不是 "[DONE]"，则视为未知格式。跳过此行，不尝试反序列化
                }

                // 至此, jsonData 应该是一个有效的 JSON 字符串块
                try
                {
                    var chunkData = JsonSerializer.Deserialize<DoubaoStreamCompletionChunk>(jsonData);
                    if (chunkData?.Choices is not { Count: > 0 }) // 使用模式匹配简化 null 检查和 Count 检查
                        continue;
                    var choice = chunkData.Choices[0];
                    // 组装单一Chunk接收到的内容，主要是分配reasoning_content和content
                    string combinedChunkText = "";

                    if (!string.IsNullOrEmpty(choice.Delta?.ReasoningContent))
                        combinedChunkText += $"<think>{choice.Delta.ReasoningContent}</think>";
                    if (!string.IsNullOrEmpty(choice.Delta?.Content))
                        combinedChunkText += choice.Delta.Content;

                    if (!string.IsNullOrEmpty(combinedChunkText))
                        requestCallBack.OnChunkReceived(combinedChunkText);

                    // 检查数据块内部是否指示了结束
                    if (string.IsNullOrEmpty(choice.FinishReason))
                        continue;
                    lastFinishReason = choice.FinishReason;
                    // 只要收到了 finish_reason (无论是 "stop" 还是其他)，就意味着这个响应流的当前部分结束了。
                    // 跳出循环，让循环后的逻辑根据 lastFinishReason 的值来决定最终结果。
                    break; // 跳出 while 循环
                }
                catch (JsonException jsonEx)
                {
                    // 对于无法解析的 JSON，记录更详细的信息
                    return AiError.Error(
                        $"解析豆包响应流时发生错误: {jsonEx.Message}. 原始JSON内容: '{jsonData[..Math.Min(200, jsonData.Length)]}'");
                }
            } // 结束 while 循环

            // 循环结束后，根据 lastFinishReason 判断最终结果
            if (lastFinishReason == "stop")
            {
                return Result.Ok(); // 流正常结束
            }

            if (!string.IsNullOrEmpty(lastFinishReason))
            {
                // 例如 "length", "content_filter", "tool_calls" 等
                // TODO 这里许多情况下不应该报错才对
                // 文档：choices.finish_reason string
                // 模型停止生成 token 的原因。取值范围：
                // stop：模型输出自然结束，或因命中请求参数 stop 中指定的字段而被截断。
                // length：模型输出因达到模型输出限制而被截断，有以下原因：
                // 触发max_token限制（回答内容的长度限制）。
                // 触发max_completion_tokens限制（思维链内容+回答内容的长度限制）。
                // 触发context_window限制（输入内容+思维链内容+回答内容的长度限制）。
                // content_filter：模型输出被内容审核拦截。
                // tool_calls：模型调用了工具。
                return AiError.Error($"豆包 AI 响应因 '{lastFinishReason}' 而提前终止。");
            }

            // 流结束了，但没有收到明确的 finish_reason (例如 [DONE] 或 chunk 内的 finish_reason)
            // 如果到这里没有抛出其他异常，并且 httpClient 和 reader 都正常结束，
            // 这可能意味着流是空的或意外截断但没有错误信号。
            // 对于某些API，这可能仍然算作成功（例如，如果请求本身就是期望空响应）。
            // 但通常，如果期望有数据流，这可能是一个潜在问题或不完整的响应。
            // 为谨慎起见，如果期望必须有 "stop" 信号，这里可以返回一个警告或错误。
            // 但当前逻辑是：如果没有错误且没有非 "stop" 的 finish_reason，则视为成功。
            return Result.Ok();
        }
        catch (HttpRequestException httpEx)
        {
            return AiError.Error($"连接豆包 API 失败: {httpEx.Message}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return AiError.Error("用户取消了 AI 请求。");
        }
        catch (Exception ex) // 捕获其他所有未预料到的异常
        {
            return AiError.Error($"调用豆包 API 时发生未知错误: {ex.GetType().Name} - {ex.Message}");
        }
    }

    public async Task<Result<string>> NonStreamRequestAsync(
        IEnumerable<RoledPromptDto> prompts,
        NonStreamRequestCallBack? requestCallBack,
        CancellationToken cancellationToken = default)
    {
        List<DoubaoChatMessage> doubaoMessages = [.. prompts.Select(p => p.ToDoubaoMessage())];

        var responseFormatParam = new DoubaoResponseFormat(this.Config.ResponseFormatType);

        var requestPayload = new DoubaoChatRequest(
            Model: this.Config.ModelName,
            Messages: doubaoMessages,
            Stream: false, // ***关键区别: 非流式请求设置为 false***
            Temperature: this.Config.Temperature,
            MaxTokens: this.Config.MaxOutputTokens,
            TopP: this.Config.TopP,
            Stop: this.Config.StopSequences,
            ResponseFormat: responseFormatParam,
            FrequencyPenalty: this.Config.FrequencyPenalty,
            PresencePenalty: this.Config.PresencePenalty,
            StreamOptions: null, // 非流式请求不需要 StreamOptions
            ServiceTier: this.Config.ServiceTier,
            Logprobs: this.Config.Logprobs,
            TopLogprobs: this.Config.TopLogprobs,
            LogitBias: this.Config.LogitBias?.ToDictionary(kvp => kvp.TokenId, kvp => kvp.BiasValue),
            Tools: null // 当前未实现工具调用
        );

        // 与流式请求相同的 URL 和 Headers 设置
        var request = new HttpRequestMessage(HttpMethod.Post, "https://ark.cn-beijing.volces.com/api/v3/chat/completions")
        {
            Content = JsonContent.Create(requestPayload,
                mediaType: new MediaTypeHeaderValue("application/json"),
                options: new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.Config.ApiKey);

        try
        {
            // 发送请求并等待完整的响应
            using var response = await this.HttpClient.SendAsync(request, cancellationToken);

            // 检查响应状态码
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                return AiError.Error(
                    $"豆包 API 非流式请求失败: {response.StatusCode}. 响应: {errorContent.Substring(0, Math.Min(500, errorContent.Length))}");
            }

            // 读取并反序列化完整的响应体
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var doubaoResponse = JsonSerializer.Deserialize<DoubaoCompletionResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true // 忽略大小写匹配属性名
            });

            // 验证响应结构和内容
            if (doubaoResponse?.Choices is not { Count: > 0 })
            {
                // 检查是否包含错误信息，例如内容过滤导致的空 choices
                // 豆包文档没有明确指出错误响应的结构，这里先返回一个通用错误
                // 如果豆包在成功状态码下返回空 choices 且在响应体中有错误字段，可能需要调整解析逻辑
                return AiError.Error($"豆包 API 返回了意外的响应结构或空结果。原始响应: {responseBody.Substring(0, Math.Min(500, responseBody.Length))}");
            }

            var choice = doubaoResponse.Choices[0]; // 获取第一个（通常是唯一一个）选择
            var message = choice.Message;

            if (message == null)
            {
                return AiError.Error(
                    $"豆包 API 响应中 choice[0] 的 message 字段为 null。原始响应: {responseBody.Substring(0, Math.Min(500, responseBody.Length))}");
            }

            // 组装最终的回复文本，包含思维链和内容
            string combinedContent = "";
            if (!string.IsNullOrEmpty(message.ReasoningContent))
            {
                // 非流式响应中也可能包含思维链，用 <think> 标签包裹
                combinedContent += $"<think>{message.ReasoningContent}</think>";
            }

            if (!string.IsNullOrEmpty(message.Content))
            {
                combinedContent += message.Content;
            }
            else if (string.IsNullOrEmpty(message.ReasoningContent))
            {
                // 如果不是内容过滤，且内容为空，则视为非预期情况
                if (string.IsNullOrEmpty(choice.FinishReason) || choice.FinishReason == "stop")
                {
                    // 如果是正常停止但内容为空，这很奇怪，可能是模型没生成内容
                    // 返回一个警告或错误，或者返回空字符串Result.Ok("")取决于期望行为
                    // 暂时返回一个警告 Result.Fail($"豆包 AI 响应成功，但未生成任何内容或思维链。")
                    return Result.Ok(combinedContent); // 返回空字符串结果，外部调用者可以检查
                }

                // 如果是非正常停止且内容为空，则返回错误
                return AiError.Error($"豆包 AI 响应因 '{choice.FinishReason}' 提前终止，且未生成任何内容或思维链。");
            }


            // 根据 finish_reason 判断结果
            // 文档：stop, length, content_filter, tool_calls
            // 与流式逻辑一致，如果 finish_reason 非空且不是 "stop"，则认为是异常或需要额外处理的情况
            if (!string.IsNullOrEmpty(choice.FinishReason) && choice.FinishReason != "stop")
            {
                // 例如 "length", "content_filter", "tool_calls"
                // 根据 StreamRequestAsync 的逻辑，这些情况都视为异常
                // 如果是 content_filter，上面的逻辑已经处理了内容为空的情况
                // 如果是 length，意味着内容被截断，可能需要返回一个警告 Result.Fail($"豆包 AI 响应因达到最大长度而截断 ('{choice.FinishReason}')，请注意内容可能不完整。").WithSuccess(combinedContent)
                // 但为了与流式逻辑一致，这里仍返回错误
                // 如果是 tool_calls，表示模型决定调用工具，这通常不是错误，而是需要特殊处理
                // 如果这里不处理工具调用，那么收到 tool_calls 作为一个非流式请求的完成原因确实需要标记
                if (choice.FinishReason == "tool_calls")
                {
                    // 如果将来支持工具调用，这里需要返回一个包含工具调用信息的结果
                    // 目前不支持，视为非预期的非流式请求完成方式
                    return AiError.Error($"豆包 AI 响应指示需要调用工具 ('{choice.FinishReason}')，但当前处理器不支持工具调用。");
                }

                // 对于 length 或 content_filter (如果上面没捕获到)，返回错误
                // content_filter 的情况下，responseBody可能仍然有结构但content为空
                return AiError.Error($"豆包 AI 响应因 '{choice.FinishReason}' 而提前终止。");
            }

            // 如果 finish_reason 是 null 或 "stop"，并且内容非空，则视为成功
            return Result.Ok(combinedContent);
        }
        catch (JsonException jsonEx)
        {
            // 反序列化失败
            // 如果 responseBody 变量可用，可以在错误信息中包含它
            return AiError.Error(
                $"解析豆包非流式响应时发生JSON错误: {jsonEx.Message}"); // 无法访问 responseBody 变量，需要修改 try 结构或捕获点
        }
        catch (HttpRequestException httpEx)
        {
            // HTTP 请求或连接错误
            return AiError.Error($"连接豆包 API 失败: {httpEx.Message}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 用户取消
            return AiError.Error("用户取消了 AI 请求。");
        }
        catch (Exception ex) // 捕获其他所有未预料到的异常
        {
            return AiError.Error($"调用豆包 API 时发生未知错误: {ex.GetType().Name} - {ex.Message}");
        }
    }
}