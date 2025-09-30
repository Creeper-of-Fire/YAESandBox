using Microsoft.Extensions.Logging;

namespace YAESandBox.Depend.Logger;

/// <inheritdoc />
internal class MicrosoftLoggerFactory(ILoggerFactory microsoftLoggerFactory) : IAppLoggerFactory
{
    private ILoggerFactory Factory { get; } = microsoftLoggerFactory;

    /// <inheritdoc />
    public IAppLogger CreateLogger(string categoryName) => new MicrosoftLogger(this.Factory.CreateLogger(categoryName));

    /// <inheritdoc />
    public IAppLogger<T> CreateLogger<T>() => new MicrosoftLogger<T>(this.Factory.CreateLogger<T>());
}

/// <inheritdoc cref="IAppLogger{T}"/>
file class MicrosoftLogger<T>(ILogger<T> microsoftLogger) : MicrosoftLogger(microsoftLogger), IAppLogger<T>;

/// <inheritdoc />
file class MicrosoftLogger(ILogger microsoftLogger) : IAppLogger
{
    private ILogger Logger { get; } = microsoftLogger;
#pragma warning disable CA2254
    /// <inheritdoc />
    public void Trace(string message, params object?[] args) =>
        this.Logger.LogTrace(message, args);

    /// <inheritdoc />
    public void Debug(string message, params object?[] args) =>
        this.Logger.LogDebug(message, args);

    /// <inheritdoc />
    public void Info(string message, params object?[] args) =>
        this.Logger.LogInformation(message, args);

    /// <inheritdoc />
    public void Warn(string message, params object?[] args) =>
        this.Logger.LogWarning(message, args);

    /// <inheritdoc />
    public void Error(string message, params object?[] args) =>
        this.Logger.LogError(message, args);

    /// <inheritdoc />
    public void Error(Exception exception, string message, params object?[] args) =>
        this.Logger.LogError(exception, message, args);

    /// <inheritdoc />
    public void Critical(string message, params object?[] args) =>
        this.Logger.LogCritical(message, args);

    /// <inheritdoc />
    public void Critical(Exception exception, string message, params object?[] args) =>
        this.Logger.LogCritical(exception, message, args);
#pragma warning restore CA2254
}