using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;
using YAESandBox.Depend.Logger;
using YAESandBox.ModuleSystem.Abstractions;

namespace YAESandBox.ModuleSystem.AspNet;

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

/// <summary>
/// 实现此接口的模块可以配置MVC相关设置
/// 用于自定义MVC框架的行为
/// </summary>
public interface IProgramModuleMvcConfigurator : IProgramModule
{
    /// <summary>
    /// 可以添加Mvc配置
    /// </summary>
    /// <param name="mvcBuilder"></param>
    void ConfigureMvc(IMvcBuilder mvcBuilder);
}

/// <summary>
/// 实现此接口的模块可以配置Swagger UI选项
/// 用于自定义API文档界面的行为和外观
/// </summary>
public interface IProgramModuleSwaggerUiOptionsConfigurator : IProgramModule
{
    /// <summary>
    /// 配置Swagger UI选项
    /// </summary>
    /// <param name="options">注入的Swagger UI配置选项</param>
    void ConfigureSwaggerUi(SwaggerUIOptions options);
}

/// <summary>
/// 实现此接口的模块可以提供额外的类型，以确保它们被包含在 OpenAPI Schema 中，
/// 即使它们没有被任何 HTTP API Controller 直接引用。
/// 这对于例如 SignalR DTOs, 共享的基类, 或其他需要通过代码生成器暴露给前端的类型非常有用。
/// </summary>
public interface IProgramModuleAdditionalSchemaProvider : IProgramModule
{
    /// <summary>
    /// 获取需要包含在 OpenAPI Schema 中的额外类型列表。
    /// </summary>
    /// <param name="context">Swagger文档过滤器上下文，可用于根据不同的文档提供不同的类型。</param>
    /// <returns>需要被包含的类型集合。</returns>
    IEnumerable<Type> GetAdditionalSchemaTypes(DocumentFilterContext context);
}


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


/// <summary>
/// 实现此接口的模块可以配置自己的静态文件服务。
/// </summary>
public interface IProgramModuleStaticAssetConfigurator : IProgramModule
{
    /// <summary>
    /// 配置模块的静态文件。
    /// </summary>
    /// <param name="app">应用程序构建器，用于注册中间件。</param>
    /// <param name="environment">Web 主机环境，用于获取内容根路径等信息。</param>
    void ConfigureStaticAssets(IApplicationBuilder app, IWebHostEnvironment environment);
}

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
public interface IProgramAtLastConfigurator : IProgramModule
{
    /// <summary>
    /// 配置最终的某些内容。
    /// </summary>
    /// <param name="context">配置的上下文。</param>
    void ConfigureAtLast(FinalConfigurationContext context);
}

/// <summary>
/// Swagger帮助类，提供Swagger文档相关的辅助方法
/// </summary>
public static class SwaggerHelper
{
    private static IAppLogger Logger { get; } = AppLogging.CreateLogger(nameof(SwaggerHelper));

    /// <summary>
    /// 为Swagger添加XML注释文档
    /// </summary>
    /// <param name="options">Swagger生成选项</param>
    /// <param name="assembly">需要添加注释的程序集</param>
    public static void AddSwaggerDocumentation(this SwaggerGenOptions options, Assembly assembly)
    {
        try
        {
            string xmlFilename = $"{assembly.GetName().Name}.xml";
            string xmlFilePath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
            if (File.Exists(xmlFilePath))
            {
                options.IncludeXmlComments(xmlFilePath);
                Logger.Info("加载 XML 注释: {XmlFilePath}", xmlFilePath);
            }
            else
            {
                Logger.Warn("警告: 未找到 XML 注释文件: {XmlFilePath}", xmlFilePath);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "加载 Contracts XML 注释时出错。");
        }
    }
}