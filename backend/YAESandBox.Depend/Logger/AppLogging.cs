using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace YAESandBox.Depend.Logger;

/// <summary>
/// 日志
/// </summary>
public static class AppLogging
{
    [field: AllowNull, MaybeNull]
    private static IAppLoggerFactory Factory
    {
        get => field ?? BootstrapFactory;
        set;
    }

    /// <summary>
    /// 一个备用的、临时的日志工厂，通常仅用于 DI 容器构建完成之前。
    /// </summary>
    private static MicrosoftLoggerFactory BootstrapFactory { get; } =
        new(LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace)));

    /// <summary>
    /// 初始化引导日志系统。
    /// </summary>
    public static void InitializeInBoostrap()
    {
        Factory = BootstrapFactory;
        var logger = BootstrapFactory.CreateLogger(nameof(AppLogging));
        logger.Info("系统初始化开始。正在使用引导日志系统。");
    }

    /// <summary>
    /// 初始化日志系统。此方法应在应用程序启动时调用一次。
    /// </summary>
    /// <param name="loggerFactory">由依赖注入容器配置和创建的 ILoggerFactory。</param>
    public static void Initialize(ILoggerFactory loggerFactory)
    {
        Factory = new MicrosoftLoggerFactory(loggerFactory);
        var logger = Factory.CreateLogger(nameof(AppLogging));
        logger.Info("系统初始化完成。正在使用自定义日志系统。");
    }

    /// <summary>
    /// 创建一个指定类别的日志记录器实例。
    /// </summary>
    /// <typeparam name="T">日志记录器关联的类型。</typeparam>
    /// <returns>一个 IAppLogger 实例。</returns>
    public static IAppLogger<T> CreateLogger<T>() => Factory.CreateLogger<T>();

    /// <summary>
    /// 创建一个非泛型的日志记录器实例。
    /// </summary>
    /// <param name="categoryName">日志的名字。</param>
    /// <returns>一个 IAppLogger 实例。</returns>
    public static IAppLogger CreateLogger(string categoryName) => Factory.CreateLogger(categoryName);
}