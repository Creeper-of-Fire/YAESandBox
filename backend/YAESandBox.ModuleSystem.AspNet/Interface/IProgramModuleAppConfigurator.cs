using Microsoft.AspNetCore.Builder;
using YAESandBox.ModuleSystem.Abstractions;

namespace YAESandBox.ModuleSystem.AspNet.Interface;

/// <summary>
/// 实现此接口的模块可以配置应用程序的中间件管道 (IApplicationBuilder)。
/// </summary>
public interface IProgramModuleAppConfigurator : IProgramModule
{
    /// <summary>
    /// 在应用构建后、运行前配置中间件。
    /// </summary>
    /// <param name="app">注入的应用程序构建器，用于配置HTTP请求管道等。</param>
    void ConfigureApp(IApplicationBuilder app);
}