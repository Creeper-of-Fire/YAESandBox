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

    public static DoubaoChatMessage ToDoubaoMessage(this (PromptRole role, string content) promptTuple)
    {
        return promptTuple.role.type switch
        {
            PromptRoleType.System => new DoubaoChatMessage("system", promptTuple.content),
            PromptRoleType.User => new DoubaoChatMessage("user", promptTuple.content, Name: promptTuple.role.name),
            PromptRoleType.Assistant => new DoubaoChatMessage("assistant", promptTuple.content),
            _ => throw new ArgumentOutOfRangeException(nameof(promptTuple.role.type), $"不支持的角色: {promptTuple.role.type}")
        };
    }
}

internal class DoubaoAiProcessor(AiProcessorDependencies dependencies, DoubaoAiProcessorConfig parameters) : IAiProcessor
{
    private readonly HttpClient _httpClient = dependencies.HttpClient;
    private readonly DoubaoAiProcessorConfig _config = parameters;

    public async Task<Result> StreamRequestAsync(
        List<(PromptRole role, string prompt)> prompts,
        Action<string> onChunkReceived,
        CancellationToken cancellationToken = default)
    {
        List<DoubaoChatMessage> doubaoMessages = [.. prompts.Select(p => p.ToDoubaoMessage())];

        var responseFormatParam = new DoubaoResponseFormat(this._config.ResponseFormatType);

        var requestPayload = new DoubaoChatRequest(
            Model: this._config.ModelName,
            Messages: doubaoMessages,
            Stream: true,
            Temperature: this._config.Temperature,
            MaxTokens: this._config.MaxTokens,
            TopP: this._config.TopP,
            Stop: this._config.StopSequences,
            ResponseFormat: responseFormatParam,
            FrequencyPenalty: this._config.FrequencyPenalty,
            PresencePenalty: this._config.PresencePenalty,
            StreamOptions: new DoubaoStreamOptions(this._config.StreamOptions_IncludeUsage),
            ServiceTier: this._config.ServiceTier,
            Logprobs: this._config.Logprobs,
            TopLogprobs: this._config.TopLogprobs,
            LogitBias: this._config.LogitBias?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Tools: null
        );

        var request = new HttpRequestMessage(HttpMethod.Post, "https://ark.cn-beijing.volces.com/api/v3/chat/completions")
        {
            Content = JsonContent.Create(requestPayload,
                mediaType: new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"),
                options: new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this._config.ApiKey); // 从 _config 获取 ApiKey

        string? lastFinishReason = null;

        try
        {
            using var response = await this._httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                // 豆包似乎不会返回错误JSON结构，而是单纯的返回错误响应
                return AiError.Error(
                    $"豆包 API 请求失败: {response.StatusCode}. 响应: {errorContent.Substring(0, Math.Min(500, errorContent.Length))}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            const string SSE_DATA_PREFIX = "data:";

            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AiError.Error("用户取消了 AI 请求。");

                if (string.IsNullOrWhiteSpace(line)) // 跳过SSE中的空行 (通常用于保持连接)
                    continue;

                string jsonData;
                if (line.StartsWith(SSE_DATA_PREFIX, StringComparison.Ordinal))
                {
                    jsonData = line.Substring(SSE_DATA_PREFIX.Length).Trim(); // 移除 "data:" 前缀并去除首尾空格

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
                    // 组装单一Chunk接收到的内容，主要是组装reasoning_content和content
                    string combinedChunkText = "";

                    if (!string.IsNullOrEmpty(choice.Delta.ReasoningContent))
                        combinedChunkText += $"<think>{choice.Delta.ReasoningContent}</think>";
                    if (!string.IsNullOrEmpty(choice.Delta.Content))
                        combinedChunkText += choice.Delta.Content;

                    if (!string.IsNullOrEmpty(combinedChunkText))
                        onChunkReceived(combinedChunkText);

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
                        $"解析豆包响应流时发生错误: {jsonEx.Message}. 原始JSON内容: '{jsonData.Substring(0, Math.Min(200, jsonData.Length))}'");
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
}