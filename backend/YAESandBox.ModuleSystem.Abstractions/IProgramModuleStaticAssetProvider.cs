namespace YAESandBox.ModuleSystem.Abstractions;

/// <summary>
/// 表示一个模块提供的静态资源定义。
/// 这个定义现在可以是基于物理路径的，也可以是基于程序集内嵌资源的。
/// </summary>
public abstract record StaticAssetDefinition
{
    /// <summary>
    /// 访问此静态资源的 URL 相对路径。例如 "/plugins/LuaScript" 或 "" (根路径)。
    /// </summary>
    public required string RequestPath { get; init; }
}

/// <summary>
/// 定义一个基于模块程序集目录下的子路径的静态资源。
/// </summary>
public record AssemblyRelativeStaticAsset(string ContentRootSubpath) : StaticAssetDefinition;

/// <summary>
/// 定义一个基于外部绝对物理路径的静态资源。
/// </summary>
public record PhysicalPathStaticAsset(string AbsolutePath) : StaticAssetDefinition;

/// <summary>
/// 实现此接口的模块可以声明它们提供的静态资源。
/// 这个接口是框架无关的，它只提供元数据，
/// 由宿主应用程序（如 ASP.NET Core）来决定如何处理这些元数据。
/// </summary>
public interface IProgramModuleStaticAssetProvider : IProgramModule
{
    /// <summary>
    /// 获取此模块提供的所有静态资源定义。
    /// </summary>
    /// <param name="serviceProvider">
    /// 服务提供者，允许在定义资源时解析依赖项，例如 IConfiguration 或 IRootPathProvider。
    /// </param>
    /// <returns>静态资源定义的集合。</returns>
    IEnumerable<StaticAssetDefinition> GetStaticAssetDefinitions(IServiceProvider serviceProvider);
}