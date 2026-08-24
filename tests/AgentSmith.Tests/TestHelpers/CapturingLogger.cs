using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0504: an <see cref="ILogger{T}"/> that keeps its warnings, so a test can assert
/// that a disagreement was REPORTED rather than swallowed.
/// </summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    public List<string> Warnings { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
    }
}
