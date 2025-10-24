using Microsoft.Extensions.DependencyInjection;
using YAESandBox.ModuleSystem.Abstractions.PluginDiscovery;
using YAESandBox.Plugin.Rune.JavaScript.Rune;
using YAESandBox.Workflow.Core.Service;

namespace YAESandBox.Plugin.Rune.JavaScript;

/// <inheritdoc cref="IYaeSandBoxPlugin"/>
public class JavaScriptPluginMain : IYaeSandBoxPlugin, IProgramModuleRuneProvider
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
    public IReadOnlyList<Type> RuneConfigTypes =>
    [
        typeof(JavaScriptRuneConfig),
        // typeof(JavaScriptStringProcessorRuneConfig)
    ];
}