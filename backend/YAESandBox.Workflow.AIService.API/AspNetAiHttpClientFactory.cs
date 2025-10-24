namespace YAESandBox.Workflow.AIService.API;

/// <summary>
/// IAiHttpClientFactory在ASP.NET Core环境下的具体实现。
/// </summary>
/// <remarks>
/// 这是一个适配器（Adapter）类。它将我们自定义的、平台无关的`IAiHttpClientFactory`接口
/// 桥接到ASP.NET Core强大的`IHttpClientFactory`系统。
/// 这样做的好处是，我们的核心业务逻辑（AIService）保持纯净，不了解ASP.NET Core，
/// 但在Web宿主中运行时，我们依然可以充分利用`IHttpClientFactory`提供的所有高级功能，
/// 例如连接池、DNS缓存管理、通过中间件处理请求（如使用Polly实现重试和熔断策略）等。
/// </remarks>
public class AspNetAiHttpClientFactory(IHttpClientFactory httpClientFactory) : IAiHttpClientFactory
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    /// <inheritdoc />
    public HttpClient CreateClient(string name)
    {
        // 将调用直接委托给ASP.NET Core的IHttpClientFactory
        return this._httpClientFactory.CreateClient(name);
    }
}