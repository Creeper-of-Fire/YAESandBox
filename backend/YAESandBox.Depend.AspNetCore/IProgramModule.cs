using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace YAESandBox.Depend.AspNetCore;

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
/// 实现此接口的模块可以提供SignalR相关的DTO类型信息
/// 用于在API文档中正确显示SignalR相关数据结构和程序生成
/// </summary>
public interface IProgramModuleSignalRTypeProvider : IProgramModule
{
    /// <summary>
    /// 获取SignalR数据传输对象类型列表
    /// </summary>
    /// <param name="document">Swagger文档过滤器上下文</param>
    /// <returns>DTO类型集合</returns>
    public IEnumerable<Type> GetSignalRDtoTypes(DocumentFilterContext document);
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
/// 为模块初始化提供所需的上下文信息。
/// </summary>
/// <param name="AllModules">所有已发现的模块实例的只读列表。</param>
/// <param name="PluginAssemblies">所有已加载的插件程序集的只读列表。</param>
public record ModuleInitializationContext(
    IReadOnlyList<IProgramModule> AllModules,
    IReadOnlyList<Assembly> PluginAssemblies
);

/// <summary>
/// 标记一个模块，表明它需要在所有模块被发现后进行一次性的初始化。
/// </summary>
public interface IProgramModuleWithInitialization : IProgramModule
{
    /// <summary>
    /// 在所有模块被发现后执行初始化逻辑。
    /// </summary>
    /// <param name="context">包含了初始化所需信息的上下文对象。</param>
    void Initialize(ModuleInitializationContext context);
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
    private static ILogger Logger { get; } = AppLogging.CreateLogger(nameof(SwaggerHelper));

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
                Logger.LogInformation("加载 XML 注释: {XmlFilePath}", xmlFilePath);
            }
            else
            {
                Logger.LogWarning("警告: 未找到 XML 注释文件: {XmlFilePath}", xmlFilePath);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "加载 Contracts XML 注释时出错: {ExMessage}", ex.Message);
        }
    }
}