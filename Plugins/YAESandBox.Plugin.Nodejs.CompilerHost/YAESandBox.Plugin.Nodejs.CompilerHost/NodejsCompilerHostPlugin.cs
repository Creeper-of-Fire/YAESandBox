using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.AspNetCore.PluginDiscovery;

namespace YAESandBox.Plugin.Nodejs.CompilerHost;

public class NodejsCompilerHostPlugin : IYaeSandBoxPlugin, IProgramModuleMvcConfigurator
{
    public PluginMetadata Metadata { get; } = new(
        Id: "YAESandBox.Plugin.Nodejs.CompilerHost",
        Name: "Node.js Compiler Host",
        Version: "1.0.0",
        Author: "Creeper_of_Fire",
        Description: "Provides an API to compile Vue/JSX components using a Node.js sidecar."
    );

    public void RegisterServices(IServiceCollection services)
    {
        // 将 CompilerService 注册为单例，因为它不持有状态且初始化成本较高
        services.AddSingleton<CompilerService>();
    }

    public void ConfigureMvc(IMvcBuilder mvcBuilder)
    {
        // 注册我们插件中的所有 Controller
        mvcBuilder.AddApplicationPart(typeof(CompilerController).Assembly);
    }
}