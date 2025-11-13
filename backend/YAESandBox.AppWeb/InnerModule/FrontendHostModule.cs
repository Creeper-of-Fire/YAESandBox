using Microsoft.Extensions.FileProviders;
using YAESandBox.Depend.Logger;
using YAESandBox.Depend.Services;
using YAESandBox.ModuleSystem.Abstractions;
using YAESandBox.ModuleSystem.AspNet;
using YAESandBox.ModuleSystem.AspNet.Interface;

namespace YAESandBox.AppWeb.InnerModule;

internal class FrontendHostModule : IProgramModuleStaticAssetProvider, IProgramModuleAtLastConfigurator
{
    private static IAppLogger Logger { get; } = AppLogging.CreateLogger<FrontendHostModule>();

    /// <inheritdoc />
    public void RegisterServices(IServiceCollection service) { }

    /// <inheritdoc />
    public void ConfigureAtLast(FinalConfigurationContext context)
    {
        string? frontendAbsolutePath = GetFrontendAbsolutePath(context.App.ApplicationServices);

        if (frontendAbsolutePath is null) return;

        // 1. 克隆我们全局配置好的 DefaultStaticFileOptions，它包含了正确的缓存策略。
        var fallbackOptions = StaticAssetModuleExtensions.DefaultStaticFileOptions.Clone();

        // 2. 为这个克隆实例设置特定的 FileProvider。
        fallbackOptions.FileProvider = new PhysicalFileProvider(frontendAbsolutePath);

        // 3. 将配置好的 options 传递给 MapFallbackToFile。
        context.EndpointBuilder.MapFallbackToFile("index.html", fallbackOptions);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 提供前端资源的静态文件服务。
    /// </remarks>
    public IEnumerable<StaticAssetDefinition> GetStaticAssetDefinitions(IServiceProvider serviceProvider)
    {
        string? frontendAbsolutePath = GetFrontendAbsolutePath(serviceProvider);

        if (frontendAbsolutePath is null) yield break;

        yield return new PhysicalPathStaticAsset(frontendAbsolutePath)
        {
            RequestPath = "" // 挂载到根目录
        };
    }

    /// <summary>
    /// 从服务容器中解析依赖，并计算出前端资源的绝对路径。
    /// </summary>
    private static string? GetFrontendAbsolutePath(IServiceProvider services)
    {
        // 1. 解析模块所需的所有服务
        var configuration = services.GetRequiredService<IConfiguration>();
        var rootPathProvider = services.GetRequiredService<IRootPathProvider>();

        // 2. 从配置中读取由外部传入的【相对路径】
        string? frontendRelativePath = configuration["FrontendRelativePath"];

        if (string.IsNullOrEmpty(frontendRelativePath))
        {
            return null;
        }

        // 3. 使用 IRootPathProvider 组合出最终的绝对路径
        string absolutePath = Path.GetFullPath(Path.Combine(rootPathProvider.RootPath, frontendRelativePath));

        if (!Directory.Exists(absolutePath))
        {
            Logger.Error("[FrontendHost] 错误: 前端路径不存在, 已尝试组合的绝对路径为: {AbsolutePath}", absolutePath);
            return null;
        }

        Logger.Info("[FrontendHost] 成功找到前端资源目录: {AbsolutePath}", absolutePath);
        return absolutePath;
    }
}