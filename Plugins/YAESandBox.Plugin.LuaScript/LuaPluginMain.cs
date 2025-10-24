using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using YAESandBox.ModuleSystem.Abstractions.PluginDiscovery;
using YAESandBox.ModuleSystem.AspNet;
using YAESandBox.Plugin.LuaScript.Rune;
using YAESandBox.Workflow.Core.Service;

namespace YAESandBox.Plugin.LuaScript;

/// <inheritdoc cref="IYaeSandBoxPlugin"/>
public class LuaPluginMain : IYaeSandBoxPlugin, IProgramModuleRuneProvider, IProgramModuleStaticAssetConfigurator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection service) { }

    /// <inheritdoc />
    public PluginMetadata Metadata { get; } = new(
        Id: "YAESandBox.Plugin.LuaScript",
        Name: "Lua 脚本插件",
        Version: "1.0.0",
        Author: "Creeper_of_Fire",
        Description: "Lua 脚本插件，拥有一个 Lua 脚本引擎，提供通用的 Lua 脚本符文和专用的字符串处理符文。"
    );

    /// <inheritdoc />
    public void ConfigureStaticAssets(IApplicationBuilder app, IWebHostEnvironment environment)
    {
        app.UseModuleWwwRoot(this); 
    }

    /// <inheritdoc />
    public IReadOnlyList<Type> RuneConfigTypes =>
    [
        typeof(LuaScriptRuneConfig),
        typeof(LuaStringProcessorRuneConfig)
    ];
}