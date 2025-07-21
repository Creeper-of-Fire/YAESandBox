using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YAESandBox.Workflow.AIService.Shared;

/// <summary>
/// 一个灵活的、可重用的客户端，用于与任何遵循"类OpenAI"API格式的服务进行通信。
/// 它通过泛型将HTTP通信逻辑与具体的数据模型解耦，从而实现了极高的可复用性。
/// </summary>
public class FlexibleOpenAiClient(HttpClient httpClient, ApiClientConfig config)
{
    private ApiClientConfig Config { get; } = config;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// 以流式方式发起聊天补全请求。
    /// </summary>
    /// <typeparam name="TRequest">请求体的数据模型类型。</typeparam>
    /// <typeparam name="TResponseChunk">流式响应中单个数据块的模型类型。</typeparam>
    /// <param name="requestPayload">请求体对象。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>一个异步可枚举序列，包含解析后的响应数据块。</returns>
    public async IAsyncEnumerable<TResponseChunk> StreamChatCompletionsAsync<TRequest, TResponseChunk>(
        TRequest requestPayload,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    ) where TRequest : class where TResponseChunk : class
    {
        var requestUri = new Uri(new Uri(this.Config.BaseUrl), "chat/completions");
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Content = JsonContent.Create(requestPayload, options: SerializerOptions);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.Config.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        await foreach (string jsonData in SseStreamProcessor.ProcessSseStreamAsync(responseStream, cancellationToken))
        {
            var chunk = JsonSerializer.Deserialize<TResponseChunk>(jsonData, SerializerOptions);
            if (chunk is not null)
            {
                yield return chunk;
            }
        }
    }

    /// <summary>
    /// 以非流式方式发起聊天补全请求。
    /// </summary>
    /// <typeparam name="TRequest">请求体的数据模型类型。</typeparam>
    /// <typeparam name="TResponse">完整响应体的数据模型类型。</typeparam>
    /// <param name="requestPayload">请求体对象。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>解析后的完整响应对象。</returns>
    public async Task<TResponse> GetChatCompletionsAsync<TRequest, TResponse>(
        TRequest requestPayload,
        CancellationToken cancellationToken = default
    ) where TRequest : class where TResponse : class
    {
        var requestUri = new Uri(new Uri(this.Config.BaseUrl), "chat/completions");
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Content = JsonContent.Create(requestPayload, options: SerializerOptions);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.Config.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TResponse>(SerializerOptions, cancellationToken) ??
               throw new InvalidOperationException();
    }
}

/// <summary>
/// 用于配置 FlexibleOpenAiClient 的数据记录。
/// </summary>
public record ApiClientConfig(string BaseUrl, string? ApiKey);