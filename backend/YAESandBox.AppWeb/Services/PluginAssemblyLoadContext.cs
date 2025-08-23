using System.Reflection;
using System.Runtime.Loader;

namespace YAESandBox.AppWeb.Services;

/// <summary>
/// 一个自定义的程序集加载上下文，用于隔离插件及其依赖。
/// 它知道如何从插件自己的目录中解析托管和非托管（原生）的依赖项。
/// </summary>
public class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>
    /// 初始化插件加载上下文。
    /// </summary>
    /// <param name="pluginPath">插件主 DLL 的完整路径。</param>
    public PluginAssemblyLoadContext(string pluginPath) : base(isCollectible: true)
    {
        // AssemblyDependencyResolver 是 .NET Core 提供的神奇工具，
        // 它会读取插件的 .deps.json 文件来智能地解析依赖关系。
        this._resolver = new AssemblyDependencyResolver(pluginPath);
    }

    /// <summary>
    /// 重写加载托管程序集的方法。
    /// 当插件代码需要加载另一个 DLL (如 NLua.dll) 时，此方法被调用。
    /// </summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 尝试使用解析器从插件目录中找到托管程序集。
        string? assemblyPath = this._resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            // 如果找到，就从该路径加载。
            return this.LoadFromAssemblyPath(assemblyPath);
        }

        // 如果在插件目录中找不到，则返回 null，
        // 这会将加载请求委托给默认的加载上下文（它知道如何找到共享的框架程序集）。
        return null;
    }

    /// <summary>
    /// 重写加载非托管（原生）库的方法。
    /// 当插件代码需要加载一个原生 DLL (如 lua54.dll) 时，此方法被调用。
    /// </summary>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        // 尝试使用解析器从插件目录（包括 runtimes 文件夹）中找到非托管库。
        string? libraryPath = this._resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            // 如果找到，就加载它并返回其句柄。
            return this.LoadUnmanagedDllFromPath(libraryPath);
        }
        
        // 如果找不到，返回 IntPtr.Zero，
        // 这会将加载请求委托给默认的加载上下文。
        return IntPtr.Zero;
    }
}