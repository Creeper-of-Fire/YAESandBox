using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using YAESandBox.Depend.AspNetCore.PluginDiscovery;

namespace YAESandBox.Depend.AspNetCore;

/// <summary>
/// 模块的静态文件服务拓展方法
/// </summary>
public static class StaticAssetModuleExtensions
{
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
        string requestPath = requestPathOverwrite ?? module.ToModuleRequestPath();

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(wwwRootPath),
            RequestPath = requestPath
        });
        Console.WriteLine($"[{moduleType.Name}] 已通过 UseModuleWwwRoot 挂载 wwwroot -> '{requestPath}'");
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