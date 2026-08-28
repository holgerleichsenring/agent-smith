using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// p0504: an <see cref="ILogger{T}"/> that keeps its warnings, so a test can assert
/// that a disagreement was REPORTED rather than swallowed.
/// <para>
/// 2026-08-25-6f12: and every line at any level, because a step the code chose to SKIP says
/// so at information — and a skip nobody can read is indistinguishable from a skip nobody
/// coded.
/// </para>
/// </summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    public List<string> Warnings { get; } = [];

    /// <summary>Every line, whatever its level.</summary>
    public List<string> Lines { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        Lines.Add(formatter(state, exception));
        if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
    }
}
