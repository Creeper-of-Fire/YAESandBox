using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.AspNetCore.PluginDiscovery;
using YAESandBox.Plugin.TextParser.Rune;
using YAESandBox.Workflow.API;

namespace YAESandBox.Plugin.TextParser;

/// <summary>
/// 文本处理器插件，提供标签解析和正则生成等高级文本处理功能。
/// </summary>
public class TextParserPluginMain : IYaeSandBoxPlugin, IProgramModuleRuneProvider, IProgramModuleMvcConfigurator,
    IProgramModuleStaticAssetConfigurator
{
    /// <inheritdoc />
    public PluginMetadata Metadata { get; } = new(
        Id: "YAESandBox.Plugin.TextParser",
        Name: "文本处理器",
        Version: "1.0.0",
        Author: "Creeper_of_Fire",
        Description: "提供基于CSS选择器的标签解析和基于正则表达式的文本生成功能。"
    );

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection service)
    {
        // 这里暂时不需要注册服务，因为 Controller 是瞬时的
    }

    /// <inheritdoc />
    public void ConfigureStaticAssets(IApplicationBuilder app, IWebHostEnvironment environment)
    {
        app.UseModuleWwwRoot(this);
    }

    /// <inheritdoc />
    public void ConfigureMvc(IMvcBuilder mvcBuilder)
    {
        // 告诉主程序加载我们这个插件程序集中的所有 Controller
        mvcBuilder.AddApplicationPart(typeof(TextParserTestController).Assembly);
    }

    /// <inheritdoc />
    public IReadOnlyList<Type> RuneConfigTypes =>
    [
        typeof(TagParserRuneConfig),
        typeof(RegexParserRuneConfig)
    ];
}