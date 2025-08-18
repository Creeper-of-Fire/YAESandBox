using System.Runtime.CompilerServices;

namespace YAESandBox.Workflow.AIService.Shared;

/// <summary>
/// 提供处理服务器发送事件 (Server-Sent Events, SSE) 流的静态方法。
/// 这是一个可重用的工具，用于从HTTP响应流中解析出有效的数据块。
/// </summary>
public static class SseStreamProcessor
{
    private const string SseDataPrefix = "data:";
    private const string SseDoneMarker = "[DONE]";

    /// <summary>
    /// 异步处理一个SSE流，并逐个返回有效的JSON数据负载。
    /// </summary>
    /// <param name="stream">从HttpClient获取的响应流。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>一个异步可枚举的字符串序列，每个字符串都是一个JSON数据块。</returns>
    public static async IAsyncEnumerable<string> ProcessSseStreamAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var streamDisposable = stream;
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue; // 跳过SSE中的空行 (用于保持连接)
            }

            if (!line.StartsWith(SseDataPrefix, StringComparison.OrdinalIgnoreCase)) 
                continue;

            string jsonData = line[SseDataPrefix.Length..].Trim();

            if (jsonData == SseDoneMarker)
            {
                yield break; // 流正常结束
            }

            if (!string.IsNullOrWhiteSpace(jsonData))
            {
                yield return jsonData;
            }
        }
    }
}