using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using YAESandBox.ModuleSystem.Abstractions;

namespace YAESandBox.ModuleSystem.AspNet.Interface;

/// <summary>
/// 为最终配置阶段提供所需的上下文。
/// </summary>
/// <param name="EndpointBuilder">用于注册最终路由的端点构建器。</param>
/// <param name="App">用于解析服务的应用程序构建器。</param>
public record FinalConfigurationContext(IEndpointRouteBuilder EndpointBuilder, IApplicationBuilder App);

/// <summary>
/// 实现此接口的模块可以在所有其他路由和端点注册后，
/// 配置最终的回退（Fallback）中间件。
/// 又或者实现其他的服务。
/// 它会在整个管道的其他部分完成后，在app.Run()方法之前执行。
/// </summary>
public interface IProgramModuleAtLastConfigurator : IProgramModule
{
    /// <summary>
    /// 配置最终的某些内容。
    /// </summary>
    /// <param name="context">配置的上下文。</param>
    void ConfigureAtLast(FinalConfigurationContext context);
}