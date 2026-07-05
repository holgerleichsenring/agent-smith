using AgentSmith.Infrastructure.Persistence.Interceptors;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0296: a cancelled DB-open (aborted request / shutdown) is expected, not a
/// failure — it must log at Debug, not FAIL/Error with a stack trace. Real
/// errors still surface at Error.
/// </summary>
public sealed class SqliteTuningInterceptorTests
{
    [Fact]
    public void LogFailure_OperationCanceled_LogsDebug()
    {
        var logger = new RecordingLogger();
        var interceptor = new SqliteTuningInterceptor(logger);
        using var conn = new SqliteConnection("Data Source=:memory:");

        interceptor.LogFailure(conn, new TaskCanceledException());

        logger.LastLevel.Should().Be(LogLevel.Debug);
    }

    [Fact]
    public void LogFailure_RealError_LogsError()
    {
        var logger = new RecordingLogger();
        var interceptor = new SqliteTuningInterceptor(logger);
        using var conn = new SqliteConnection("Data Source=:memory:");

        interceptor.LogFailure(conn, new InvalidOperationException("disk full"));

        logger.LastLevel.Should().Be(LogLevel.Error);
    }

    private sealed class RecordingLogger : ILogger<SqliteTuningInterceptor>
    {
        public LogLevel? LastLevel { get; private set; }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => LastLevel = logLevel;

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
