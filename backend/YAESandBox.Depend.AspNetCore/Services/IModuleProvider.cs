namespace YAESandBox.Depend.AspNetCore.Services;

/// <summary>
/// 提供对应用程序中所有已发现和加载的模块的访问。
/// 这是一个在启动时填充并注册为单例的服务。
/// </summary>
public interface IModuleProvider
{
    /// <summary>
    /// 获取所有已加载模块的只读列表。
    /// </summary>
    IReadOnlyList<IProgramModule> AllModules { get; }
}

/// <summary>
/// IModuleProvider 的默认实现。
/// </summary>
public class ModuleProvider(IReadOnlyList<IProgramModule> modules) : IModuleProvider
{
    /// <inheritdoc />
    public IReadOnlyList<IProgramModule> AllModules { get; } = modules ?? throw new ArgumentNullException(nameof(modules));
}