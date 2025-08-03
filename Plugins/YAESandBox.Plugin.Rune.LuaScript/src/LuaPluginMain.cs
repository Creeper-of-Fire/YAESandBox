using Microsoft.Extensions.DependencyInjection;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.AspNetCore.PluginDiscovery;
using YAESandBox.Workflow.API;
using YAESandBox.Workflow.Rune.ExactRune;

namespace YAESandBox.Plugin.Lua;

/// <inheritdoc cref="IYaeSandBoxPlugin"/>
public class LuaPluginMain : IYaeSandBoxPlugin, IProgramModuleRuneProvider
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection service) { }

    /// <inheritdoc />
    public PluginMetadata Metadata { get; } = new(
        Id: "YAESandBox.Plugin.Lua",
        Name: "Lua 脚本插件",
        Version: "1.0.0",
        Author: "Your Name",
        Description: "Lua 脚本插件，拥有一个 Lua 脚本引擎，并且可以提供一个 Lua 脚本符文。"
    );

    /// <inheritdoc />
    public IReadOnlyList<Type> RuneConfigTypes => [typeof(LuaScriptRuneConfig)];
}