using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-02-0d72: every entry a composition writes, with its level and rendered message — for a
/// case whose subject is what the server wrote down, not only what it answered.
/// </summary>
internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<(string Category, LogLevel Level, string Message)> _entries = new();

    public IReadOnlyList<(string Category, LogLevel Level, string Message)> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) => new Recorder(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class Recorder(
        string category, ConcurrentQueue<(string, LogLevel, string)> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Enqueue((category, logLevel, formatter(state, exception)));
    }
}
