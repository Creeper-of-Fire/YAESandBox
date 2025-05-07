namespace YAESandBox.Workflow.AIService;

/// <summary>
/// 这个是内部新建Ai服务类时使用的东西。
/// </summary>
internal abstract record AbstractAiProcessorConfig(string ConfigName,string UUID, string ModuleType) : IAiProcessorConfig
{
    public abstract IAiProcessor ToAiProcessor(AiProcessorDependencies dependencies);
}

/// <summary>
/// 这个是给外部调用的服务端口
/// </summary>
internal interface IAiProcessorConfig
{
    public IAiProcessor ToAiProcessor(AiProcessorDependencies dependencies);
}


/// <summary>
/// 封装创建 IAiProcessor 时可能需要的共享依赖项
/// </summary>
/// <param name="HttpClient">用于创建 HttpClient 实例。每个 AI 服务需要特定配置的 HttpClient，MasterAiService 应该负责创建并传递。</param>
public record AiProcessorDependencies(HttpClient HttpClient);