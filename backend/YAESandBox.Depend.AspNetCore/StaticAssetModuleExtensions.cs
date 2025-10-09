using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles.Infrastructure;
using Microsoft.Extensions.FileProviders;
using YAESandBox.Depend.AspNetCore.PluginDiscovery;
using YAESandBox.Depend.Logger;

namespace YAESandBox.Depend.AspNetCore;

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
            var path = ctx.Context.Request.Path.Value ?? string.Empty;

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
    /// 为实现了 IProgramModule 的模块挂载其内嵌的 "wwwroot" 目录作为静态文件服务。
    /// 这是为插件和模块提供前端资源的标准、便捷方式。
    /// </summary>
    /// <param name="app">应用程序构建器。</param>
    /// <param name="module">要挂载资源的模块实例。</param>
    /// <param name="requestPathOverwrite">前端访问此资源的 URL 路径。如果为 null，则默认为 "/plugins/{ModuleName}"。</param>
    public static void UseModuleWwwRoot(this IApplicationBuilder app, IProgramModule module, string? requestPathOverwrite = null)
    {
        var moduleType = module.GetType();
        var assembly = moduleType.Assembly;
        string? assemblyPath = Path.GetDirectoryName(assembly.Location);

        if (string.IsNullOrEmpty(assemblyPath)) return;

        string wwwRootPath = Path.Combine(assemblyPath, "wwwroot");

        if (!Directory.Exists(wwwRootPath)) return;

        // 如果未提供请求路径，则根据模块名称自动生成一个
        // 空字串表示根目录，依旧是有效的，所以我们这里只判断 null，不判断是否是空字符串
        string requestPath = requestPathOverwrite ?? module.ToRequestPath();

        app.UseStaticFiles(DefaultStaticFileOptions.CloneAndReBond(
            fileProvider: new PhysicalFileProvider(wwwRootPath),
            requestPath: requestPath)
        );
        Logger.Info("[{ModuleTypeName}] 已通过 UseModuleWwwRoot 挂载 wwwroot: {WwwRootPath} -> '{RequestPath}'",
            moduleType.Name, wwwRootPath, requestPath);
    }

    /// <summary>
    /// 获得模块对应的前端请求路径
    /// </summary>
    /// <param name="module"></param>
    /// <returns></returns>
    public static string ToRequestPath(this IProgramModule module) =>
        module is IYaeSandBoxPlugin plugin ? plugin.ToPluginRequestPath() : module.ToModuleRequestPath();

    private static string ToModuleRequestPath(this IProgramModule module) => $"/plugins/{module.GetType().Name}";

    private static string ToPluginRequestPath(this IYaeSandBoxPlugin plugin) => $"/plugins/{plugin.Metadata.Id}";
}