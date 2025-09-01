using Microsoft.Extensions.FileProviders;
using YAESandBox.Depend;
using YAESandBox.Depend.AspNetCore;
using YAESandBox.Depend.AspNetCore.Services;

namespace YAESandBox.AppWeb.InnerModule;

internal class FrontendHostModule : IProgramModuleStaticAssetConfigurator, IProgramAtLastConfigurator
{
    private static ILogger Logger { get; } = AppLogging.CreateLogger<FrontendHostModule>();
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection service) { }

    /// <inheritdoc />
    public void ConfigureAtLast(FinalConfigurationContext context)
    {
        string? frontendAbsolutePath = GetFrontendAbsolutePath(context.App.ApplicationServices);

        if (frontendAbsolutePath == null) return;

        context.EndpointBuilder.MapFallbackToFile("index.html", new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(frontendAbsolutePath)
        });
    }

    /// <inheritdoc />
    public void ConfigureStaticAssets(IApplicationBuilder app, IWebHostEnvironment environment)
    {
        string? frontendAbsolutePath = GetFrontendAbsolutePath(app.ApplicationServices);

        if (frontendAbsolutePath == null) return;

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(frontendAbsolutePath),
            RequestPath = ""
        });
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
            Logger.LogError("[FrontendHost] 错误: 前端路径不存在, 已尝试组合的绝对路径为: {AbsolutePath}", absolutePath);
            return null;
        }

        Logger.LogInformation("[FrontendHost] 成功找到前端资源目录: {AbsolutePath}", absolutePath);
        return absolutePath;
    }
}