using Microsoft.Extensions.Logging;

namespace FlyingAzure;

/// <summary>
/// Minimal <see cref="ILoggerProvider"/> that appends to a log file. A screensaver
/// has no Generic Host, and Microsoft.Extensions.Logging ships no built-in file
/// sink, so this is the small idiomatic bridge to <see cref="ILogger"/>.
/// </summary>
public sealed class FileLoggerProvider(string path) : ILoggerProvider
{
    private readonly Lock _gate = new();

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose() { }

    private void Write(string line)
    {
        try
        {
            lock (_gate)
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must never throw into the caller (especially from a crash handler).
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string line = $"[{DateTime.Now:O}] {logLevel,-11} {category} - {formatter(state, exception)}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            provider.Write(line);
        }
    }
}
