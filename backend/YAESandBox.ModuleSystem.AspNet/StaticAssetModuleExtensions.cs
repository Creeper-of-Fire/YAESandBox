using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles.Infrastructure;
using Microsoft.Extensions.FileProviders;
using YAESandBox.Depend.Logger;
using YAESandBox.ModuleSystem.Abstractions;

namespace YAESandBox.ModuleSystem.AspNet;

/// <summary>
/// 模块的静态文件服务拓展方法
/// </summary>
public static class StaticAssetModuleExtensions
{
    private static IAppLogger Logger { get; } = AppLogging.CreateLogger(nameof(StaticAssetModuleExtensions));

    /// <summary>
    /// 项目的默认静态文件配置
    /// </summary>
    public static StaticFileOptions DefaultStaticFileOptions { get; } = new()
    {
        // 允许提供未知文件类型的文件
        ServeUnknownFileTypes = true,

        // 为所有未知文件类型设置默认的 Content-Type
        // 'text/plain' 是一个安全且通用的选择
        DefaultContentType = "text/plain",

        // 添加 OnPrepareResponse 回调来动态设置缓存头
        OnPrepareResponse = ctx =>
        {
            var headers = ctx.Context.Response.Headers;
            string path = ctx.Context.Request.Path.Value ?? string.Empty;

            // 检查请求的是否是 index.html
            // 我们检查路径是否为根路径 "/" 或者以 ".html" 结尾
            // 这样可以同时覆盖 MapFallbackToFile 和直接请求 index.html 的情况
            if (path.EndsWith('/') || path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || path == string.Empty)
            {
                // 如果是 index.html，则强制浏览器不要缓存
                headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                headers.Append("Pragma", "no-cache");
                headers.Append("Expires", "0");
            }
        }
    };

    /// <summary>
    /// 创建一个 StaticFileOptions 对象的浅克隆。
    /// 这允许我们重用一个通用的模板配置，并为每个特定用途覆盖个别属性。
    /// </summary>
    /// <param name="options">要克隆的源对象。</param>
    /// <returns>一个新的 StaticFileOptions 实例，其属性与源对象相同。</returns>
    public static StaticFileOptions Clone(this StaticFileOptions options)
    {
        var newSharedOptions = new SharedOptions
        {
            RedirectToAppendTrailingSlash = options.RedirectToAppendTrailingSlash
        };

        var newOptions = new StaticFileOptions(newSharedOptions)
        {
            // 手动将所有相关属性从源对象复制到一个新实例中
            ContentTypeProvider = options.ContentTypeProvider,
            DefaultContentType = options.DefaultContentType,
            ServeUnknownFileTypes = options.ServeUnknownFileTypes,
            OnPrepareResponse = options.OnPrepareResponse,
            OnPrepareResponseAsync = options.OnPrepareResponseAsync,
            HttpsCompression = options.HttpsCompression
        };
        return newOptions;
    }

    /// <summary>
    /// 创建一个 StaticFileOptions 对象的浅克隆。并且为每个特定用途覆盖个别属性。
    /// 这允许我们重用一个通用的模板配置，并为每个特定用途覆盖个别属性。
    /// </summary>
    /// <param name="options">要克隆的源对象。</param>
    /// <param name="fileProvider"></param>
    /// <param name="requestPath"></param>
    /// <returns>一个新的 StaticFileOptions 实例，其属性与源对象相同。</returns>
    public static StaticFileOptions CloneAndReBond(this StaticFileOptions options, IFileProvider fileProvider, PathString requestPath)
    {
        var newOptions = options.Clone();
        newOptions.RequestPath = requestPath;
        newOptions.FileProvider = fileProvider;
        return newOptions;
    }

    /// <summary>
    /// 为实现了 IProgramModuleStaticAssetProvider 的模块配置静态文件服务。
    /// </summary>
    public static void ConfigureStaticAssets(this IProgramModuleStaticAssetProvider provider, IApplicationBuilder app)
    {
        // 1. 从 IApplicationBuilder 中获取 IServiceProvider
        var services = app.ApplicationServices;

        // 2. 调用模块的框架无关方法，传入 IServiceProvider
        var assetDefinitions = provider.GetStaticAssetDefinitions(services);

        // 3. 在这个 AspNetCore 感知的方法中，处理所有具体的中间件注册逻辑
        foreach (var asset in assetDefinitions)
        {
            IFileProvider? fileProvider = null;
            string? physicalPathLog = null;

            switch (asset)
            {
                case AssemblyRelativeStaticAsset assemblyAsset:
                    var assembly = provider.GetType().Assembly;
#pragma warning disable IL3000 // 'System.Reflection.Assembly.Location' is incompatible with single-file publishing.
                    string? assemblyPath = Path.GetDirectoryName(assembly.Location);
#pragma warning restore IL3000 // 'System.Reflection.Assembly.Location' is incompatible with single-file publishing.
                    // 检查路径是否为空。如果为空，说明可能处于单文件发布等无法获取物理路径的场景。
                    if (string.IsNullOrEmpty(assemblyPath))
                    {
                        Logger.Warn("无法获取模块 [{ModuleName}] 的程序集路径，已跳过。", provider.GetType().Name);
                        continue;
                    }

                    string physicalContentPath = Path.Combine(assemblyPath, assemblyAsset.ContentRootSubpath);
                    if (Directory.Exists(physicalContentPath))
                    {
                        fileProvider = new PhysicalFileProvider(physicalContentPath);
                        physicalPathLog = physicalContentPath;
                    }

                    break;

                case PhysicalPathStaticAsset physicalAsset:
                    if (Directory.Exists(physicalAsset.AbsolutePath))
                    {
                        fileProvider = new PhysicalFileProvider(physicalAsset.AbsolutePath);
                        physicalPathLog = physicalAsset.AbsolutePath;
                    }

                    break;

                default:
                    continue; // 跳过未知的定义类型
            }

            if (fileProvider is null)
            {
                Logger.Warn("模块 [{ModuleName}] 声明的静态资源路径不存在或无法解析，已跳过。定义: {@AssetDefinition}",
                    provider.GetType().Name, asset);
                continue;
            }

            app.UseStaticFiles(DefaultStaticFileOptions.CloneAndReBond(
                fileProvider: fileProvider,
                requestPath: asset.RequestPath
            ));
            Logger.Info("模块 [{ModuleName}] 已挂载静态资源: {PhysicalPath} -> '{RequestPath}'",
                provider.GetType().Name, physicalPathLog, asset.RequestPath);
        }
    }
}