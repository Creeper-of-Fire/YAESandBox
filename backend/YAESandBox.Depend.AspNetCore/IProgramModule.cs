using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace YAESandBox.Depend.AspNetCore;

public interface IProgramModule
{
    /// <summary>
    /// 注册 DI 服务和其他杂项
    /// </summary>
    /// <param name="service"></param>
    public void RegisterServices(IServiceCollection service);
}

/// <summary>
/// 实现此接口的模块可以配置应用程序的中间件管道 (IApplicationBuilder)。
/// </summary>
public interface IProgramModuleAppConfigurator: IProgramModule
{
    /// <summary>
    /// 在应用构建后、运行前配置中间件。
    /// </summary>
    /// <param name="app">应用程序构建器。</param>
    void ConfigureApp(IApplicationBuilder app);
}

public interface IProgramModuleMvcConfigurator: IProgramModule
{
    /// <summary>
    /// 可以添加Mvc配置
    /// </summary>
    /// <param name="mvcBuilder"></param>
    void ConfigureMvc(IMvcBuilder mvcBuilder);
}

public interface IProgramModuleSwaggerUiOptionsConfigurator: IProgramModule
{
    void ConfigureSwaggerUi(SwaggerUIOptions options);
}

public interface IProgramModuleSignalRTypeProvider: IProgramModule
{
    public IEnumerable<Type> GetSignalRDtoTypes(DocumentFilterContext document);
}

public interface IProgramModuleHubRegistrar: IProgramModule
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

public record SwaggerModuleInfo(string GroupName);

public static class SwaggerHelper
{
    public static void AddSwaggerDocumentation(this SwaggerGenOptions options, Assembly assembly)
    {
        try
        {
            string xmlFilename = $"{assembly.GetName().Name}.xml";
            string xmlFilePath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
            if (File.Exists(xmlFilePath))
            {
                options.IncludeXmlComments(xmlFilePath);
                Console.WriteLine($"加载 XML 注释: {xmlFilePath}");
            }
            else
            {
                Console.WriteLine($"警告: 未找到 XML 注释文件: {xmlFilePath}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载 Contracts XML 注释时出错: {ex.Message}");
        }
    }
}