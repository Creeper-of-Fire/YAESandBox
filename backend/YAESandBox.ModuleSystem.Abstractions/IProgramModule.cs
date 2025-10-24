using Microsoft.Extensions.DependencyInjection;

namespace YAESandBox.ModuleSystem.Abstractions;

/// <summary>
/// 程序模块的基础接口，所有模块都需要实现此接口
/// 用于在应用程序启动时注册依赖注入服务和其他配置
/// </summary>
public interface IProgramModule
{
    /// <summary>
    /// 注册 DI 服务和其他杂项
    /// </summary>
    /// <param name="service">注入的服务集合，用于注册依赖注入服务</param>
    public void RegisterServices(IServiceCollection service);
}
