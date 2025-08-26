using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using YAESandBox.Workflow.AIService.Shared;

namespace YAESandBox.Workflow.AIService.AiConfig.Gemini;

/// <summary>
/// 专门用于与 Google Gemini API 通信的客户端。
/// 处理 Gemini 特定的 URL 结构、身份验证和流式响应格式。
/// </summary>
internal class GeminiApiClient(HttpClient httpClient, ApiClientConfig config)
{
    private ApiClientConfig Config { get; } = config;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Google Gemini API 倾向于使用 camelCase 命名约定
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// 以流式方式发起聊天补全请求。
    /// </summary>
    /// <typeparam name="TRequest">请求体的数据模型类型。</typeparam>
    /// <typeparam name="TResponseChunk">流式响应中单个数据块的模型类型。</typeparam>
    /// <param name="requestPayload">请求体对象。</param>
    /// <param name="modelName">要调用的模型名称（用于构建 URL）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>一个异步可枚举序列，包含解析后的响应数据块。</returns>
    public async IAsyncEnumerable<TResponseChunk> StreamChatCompletionsAsync<TRequest, TResponseChunk>(
        TRequest requestPayload,
        string modelName,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    ) where TRequest : class where TResponseChunk : class
    {
        var requestUri = BuildRequestUri(modelName, stream: true);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Content = JsonContent.Create(requestPayload, options: SerializerOptions);
        // Gemini 不使用 Bearer Token 进行 API Key 身份验证，而是使用 x-goog-api-key 标头或查询参数。
        // 这里我们优先使用标头（更安全）。
        request.Headers.Add("x-goog-api-key", this.Config.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        // 复用 SseStreamProcessor 来处理 SSE 事件流
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
    /// <param name="modelName">要调用的模型名称（用于构建 URL）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>解析后的完整响应对象。</returns>
    public async Task<TResponse> GetChatCompletionsAsync<TRequest, TResponse>(
        TRequest requestPayload,
        string modelName,
        CancellationToken cancellationToken = default
    ) where TRequest : class where TResponse : class
    {
        var requestUri = BuildRequestUri(modelName, stream: false);
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Content = JsonContent.Create(requestPayload, options: SerializerOptions);
        request.Headers.Add("x-goog-api-key", this.Config.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TResponse>(SerializerOptions, cancellationToken) ??
               throw new InvalidOperationException("无法从 Gemini API 响应中反序列化内容。");
    }

    /// <summary>
    /// 构建 Gemini API 的请求 URL。
    /// 格式: {baseUrl}/models/{modelName}:{action}
    /// </summary>
    private string BuildRequestUri(string modelName, bool stream)
    {
        var action = stream ? "streamGenerateContent" : "generateContent";
        // 确保 BaseUrl 尾部有斜杠
        var baseUrl = this.Config.BaseUrl.EndsWith('/') ? this.Config.BaseUrl : this.Config.BaseUrl + "/";

        // 如果使用查询参数传递key（备选方案）：
        // return $"{baseUrl}models/{modelName}:{action}?key={this.Config.ApiKey}";

        return $"{baseUrl}models/{modelName}:{action}";
    }
}