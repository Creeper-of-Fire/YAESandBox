using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.AspNetCore.PluginDiscovery;
using YAESandBox.Plugin.Rune.JavaScript.Rune;
using YAESandBox.Workflow.API;

namespace YAESandBox.Plugin.Rune.JavaScript;

/// <inheritdoc cref="IYaeSandBoxPlugin"/>
public class JavaScriptPluginMain : IYaeSandBoxPlugin, IProgramModuleRuneProvider, IProgramModuleStaticAssetConfigurator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection service) { }

    /// <inheritdoc />
    public PluginMetadata Metadata { get; } = new(
        Id: "YAESandBox.Plugin.Rune.JavaScript",
        Name: "JavaScript 脚本插件",
        Version: "1.0.0",
        Author: "Creeper_of_Fire",
        Description: "JavaScript 脚本插件，使用 Jint 引擎，提供通用的 JS 脚本符文和专用的字符串处理符文。"
    );

    /// <inheritdoc />
    public void ConfigureStaticAssets(IApplicationBuilder app, IWebHostEnvironment environment)
    {
        app.UseModuleWwwRoot(this); 
    }

    /// <inheritdoc />
    public IReadOnlyList<Type> RuneConfigTypes =>
    [
        typeof(JavaScriptRuneConfig),
        // typeof(JavaScriptStringProcessorRuneConfig)
    ];
}