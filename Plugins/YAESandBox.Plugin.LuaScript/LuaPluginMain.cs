using Microsoft.Extensions.DependencyInjection;
using YAESandBox.ModuleSystem.Abstractions;
using YAESandBox.ModuleSystem.Abstractions.PluginDiscovery;
using YAESandBox.Plugin.LuaScript.Rune;
using YAESandBox.Workflow.Core.Service;

namespace YAESandBox.Plugin.LuaScript;

/// <inheritdoc cref="IYaeSandBoxPlugin"/>
public class LuaPluginMain : IYaeSandBoxPlugin, IProgramModuleRuneProvider, IProgramModuleStaticAssetProvider
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
    public IEnumerable<StaticAssetDefinition> GetStaticAssetDefinitions(IServiceProvider serviceProvider)
    {
        yield return new AssemblyRelativeStaticAsset("wwwroot")
        {
            RequestPath = this.ToRequestPath()
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<Type> RuneConfigTypes =>
    [
        typeof(LuaScriptRuneConfig),
        typeof(LuaStringProcessorRuneConfig)
    ];
}