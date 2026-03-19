using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public sealed class FileLoggerProvider(string path) : ILoggerProvider
{
    private readonly StreamWriter _writer = new(path, append: true) { AutoFlush = true };

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _writer);

    public void Dispose() => _writer.Dispose();
}

public sealed class FileLogger(string category, StreamWriter writer) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        lock (writer)
        {
            writer.WriteLine($"{DateTime.Now:HH:mm:ss.fff} [{logLevel,-12}] {category}: {formatter(state, exception)}");
            if (exception is not null)
                writer.WriteLine(exception.ToString());
        }
    }
}
