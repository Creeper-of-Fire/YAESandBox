namespace YAESandBox.Workflow.AIService;

/// <summary>
/// 为AI服务提供专用的HttpClient实例的抽象工厂。
/// </summary>
/// <remarks>
/// 此接口的核心目的是将AI服务模块与具体的HttpClient创建和管理机制解耦。
/// 通过定义这个专用的工厂，我们可以避免直接依赖于ASP.NET Core的`IHttpClientFactory`，
/// 使得`MasterAiService`及其相关逻辑变得平台无关，可以轻松地在非Web环境（如桌面应用、控制台、移动端）中重用。
/// 在不同的宿主环境中，可以提供此接口的不同实现，从而适配该环境的最佳实践。
/// </remarks>
public interface IAiHttpClientFactory
{
    /// <summary>
    /// 创建并返回一个用于特定AI服务的HttpClient实例。
    /// </summary>
    /// <param name="name">
    /// 客户端的逻辑名称。这个名称可以用于从工厂中获取一个预先配置好的HttpClient。
    /// 例如，"OpenAI", "Gemini", "DeepSeek"等，每个名称可能对应着不同的BaseAddress、DefaultHeaders或HttpMessageHandler配置。
    /// </param>
    /// <returns>一个配置好的 <see cref="HttpClient"/> 实例。</returns>
    HttpClient CreateClient(string name);
}