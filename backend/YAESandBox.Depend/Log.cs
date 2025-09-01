using Microsoft.Extensions.Logging;

namespace YAESandBox.Depend;

/// <summary>
/// 日志
/// </summary>
public static class AppLogging
{
    private static ILoggerFactory? Factory { get; set; }

    /// <summary>
    /// 初始化日志系统。此方法应在应用程序启动时调用一次。
    /// </summary>
    /// <param name="loggerFactory">由依赖注入容器配置和创建的 ILoggerFactory。</param>
    public static void Initialize(ILoggerFactory loggerFactory)
    {
        Factory = loggerFactory;
    }

    /// <summary>
    /// 创建一个指定类别的日志记录器实例。
    /// </summary>
    /// <typeparam name="T">日志记录器关联的类型。</typeparam>
    /// <returns>一个 ILogger&lt;T&gt; 实例。</returns>
    public static ILogger<T> CreateLogger<T>()
    {
        // 如果工厂未初始化，则抛出异常，这有助于在开发早期发现配置错误。
        if (Factory == null)
        {
            throw new InvalidOperationException("AppLogging尚未初始化。请在程序启动时调用 Initialize() 方法。");
        }

        return Factory.CreateLogger<T>();
    }

    /// <summary>
    /// 创建一个非泛型的日志记录器实例。
    /// </summary>
    /// <param name="categoryName">日志的名字。</param>
    /// <returns>一个 ILogger 实例。</returns>
    public static ILogger CreateLogger(string categoryName)
    {
        if (Factory == null)
        {
            throw new InvalidOperationException("AppLogging尚未初始化。请在程序启动时调用 Initialize() 方法。");
        }

        return Factory.CreateLogger(categoryName);
    }
}