using Microsoft.AspNetCore.Routing;
using YAESandBox.ModuleSystem.Abstractions;

namespace YAESandBox.ModuleSystem.AspNet.Interface;

/// <summary>
/// 实现此接口的模块可以注册SignalR Hub
/// 用于将SignalR实时通信功能集成到应用程序中
/// </summary>
public interface IProgramModuleHubRegistrar : IProgramModule
{
    /// <summary>
    /// 在应用程序的端点路由中映射此模块提供的 SignalR Hub。
    /// </summary>
    /// <param name="endpoints">应用程序的端点路由构建器。</param>
    void MapHubs(IEndpointRouteBuilder endpoints);
}