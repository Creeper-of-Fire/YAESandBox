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
    public void Info(string message)
    {
        Console.WriteLine(message);
    }

    public void Warning(string message)
    {
        Console.WriteLine(message);
    }

    public void Error(string message)
    {
        Console.WriteLine(message);
    }

    public void Debug(string message)
    {
        Console.WriteLine(message);
    }
}

public static class Log
{
    private static readonly Logger _logger = new();

    public static void Info(string message)
    {
        _logger.Info(message);
    }

    public static void Warning(string message)
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