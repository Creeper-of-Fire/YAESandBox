namespace YAESandBox.Depend;

public interface ILogger
{
    void Info(string message);
    void Warning(string message);
    void Error(string message);
    void Debug(string message);
}

public class Logger : ILogger
{
    public virtual void Info(string message)
    {
        Console.WriteLine(message);
    }

    public virtual void Warning(string message)
    {
        Console.WriteLine(message);
    }

    public virtual void Error(string message)
    {
        Console.WriteLine(message);
    }

    public virtual void Debug(string message)
    {
        Console.WriteLine(message);
    }
}

public class Logger<T> : Logger, ILogger<T>
{
    public override void Info(string message)
    {
        base.Info($"{nameof(T)}: {message}");
    }

    public override void Warning(string message)
    {
        Console.WriteLine($"{nameof(T)}: {message}");
    }

    public override void Error(string message)
    {
        Console.WriteLine($"{nameof(T)}: {message}");
    }

    public override void Debug(string message)
    {
        Console.WriteLine($"{nameof(T)}: {message}");
    }
}

public interface ILogger<T>
{
    void Info(string message);
    void Warning(string message);
    void Error(string message);
    void Debug(string message);
}

public static class Log
{
    private static readonly Logger _logger = new();
    public static ILogger<T> CreateLogger<T>()
    {
        return new Logger<T>();
    }

    public static void Info(string message)
    {
        _logger.Info(message);
    }

    public static void Warning(string message)
    {
        _logger.Warning(message);
    }

    public static void Warning(Exception exception, string message)
    {
        _logger.Warning(message);
    }


    public static void Error(string message)
    {
        _logger.Error(message);
    }

    public static void Error(Exception exception, string message)
    {
        _logger.Error(message);
    }

    public static void Debug(string message)
    {
        _logger.Debug(message);
    }
}