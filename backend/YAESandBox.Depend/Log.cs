namespace YAESandBox.Depend;

file interface ILogger
{
    /// <summary> 消息 </summary>
    void Info(string message);

    /// <summary> 警告 </summary>
    void Warning(string message);

    /// <summary> 错误 </summary>
    void Error(string message);

    /// <summary> 调试 </summary>
    void Debug(string message);
}

internal class Logger : ILogger
{
    /// <inheritdoc/>
    public virtual void Info(string message)
    {
        Console.WriteLine(message);
    }

    /// <inheritdoc/>
    public virtual void Warning(string message)
    {
        Console.WriteLine(message);
    }

    /// <inheritdoc/>
    public virtual void Error(string message)
    {
        Console.WriteLine(message);
    }

    /// <inheritdoc/>
    public virtual void Debug(string message)
    {
        Console.WriteLine(message);
    }
}

file class Logger<T> : Logger, ILogger<T>
{
    /// <inheritdoc cref="ILogger{T}.Info" />
    public override void Info(string message)
    {
        base.Info($"{nameof(T)}: {message}");
    }

    /// <inheritdoc cref="ILogger{T}.Info" />
    public override void Warning(string message)
    {
        Console.WriteLine($"{nameof(T)}: {message}");
    }

    /// <inheritdoc cref="ILogger{T}.Info" />
    public override void Error(string message)
    {
        Console.WriteLine($"{nameof(T)}: {message}");
    }

    /// <inheritdoc cref="ILogger{T}.Info" />
    public override void Debug(string message)
    {
        Console.WriteLine($"{nameof(T)}: {message}");
    }
}

/// <summary>
/// 日志接口
/// </summary>
/// <typeparam name="T"></typeparam>
public interface ILogger<T>
{
    /// <summary> 消息 </summary>
    void Info(string message);

    /// <summary> 警告 </summary>
    void Warning(string message);

    /// <summary> 错误 </summary>
    void Error(string message);

    /// <summary> 调试 </summary>
    void Debug(string message);
}

/// <summary>
/// 日志
/// </summary>
public static class Log
{
    private static readonly Logger Logger = new();

    /// <summary> 创建日志实例 (泛型,输出带有类 <typeparamref name="T"/> 自身的信息) </summary>
    /// <typeparam name="T">一般为所有者的类型</typeparam>
    public static ILogger<T> CreateLogger<T>() => new Logger<T>();

    /// <inheritdoc cref="ILogger{T}.Info(string)"/>
    public static void Info(string message)
    {
        Logger.Info(message);
    }

    /// <inheritdoc cref="ILogger{T}.Warning(string)"/>
    public static void Warning(string message)
    {
        Logger.Warning(message);
    }

    /// <inheritdoc cref="ILogger{T}.Warning(string)"/>
    public static void Warning(Exception exception, string message)
    {
        Logger.Warning(message);
    }

    /// <inheritdoc cref="ILogger{T}.Error(string)"/>
    public static void Error(string message)
    {
        Logger.Error(message);
    }

    /// <inheritdoc cref="ILogger{T}.Error(string)"/>
    public static void Error(Exception exception, string message)
    {
        Logger.Error(message);
    }

    /// <inheritdoc cref="ILogger{T}.Debug(string)"/>
    public static void Debug(string message)
    {
        Logger.Debug(message);
    }
}