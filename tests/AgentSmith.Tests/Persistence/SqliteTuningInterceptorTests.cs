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
    // p0348: busy_timeout must precede journal_mode so the lock-contended
    // DELETE->WAL conversion the first server connection performs waits out the
    // grace instead of failing instantly with SQLITE_BUSY at boot.
    [Fact]
    public void TunePragmas_SetsBusyTimeoutBeforeJournalMode()
    {
        var pragmas = SqliteTuningInterceptor.TunePragmas;
        var busy = pragmas.IndexOf("busy_timeout", StringComparison.OrdinalIgnoreCase);
        var journal = pragmas.IndexOf("journal_mode", StringComparison.OrdinalIgnoreCase);
        busy.Should().BeGreaterThan(-1);
        journal.Should().BeGreaterThan(busy, "the busy timeout must be in effect before the WAL conversion");
    }

    // p0348: applying the tuning leaves the file in WAL — the mode the migrate
    // one-shot must persist so the server never performs the conversion at boot.
    [Fact]
    public void TunePragmas_LeavesFileInWalMode()
    {
        var path = Path.Combine(Path.GetTempPath(), $"as-wal-{Guid.NewGuid():N}.db");
        try
        {
            using (var conn = new SqliteConnection($"Data Source={path}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = SqliteTuningInterceptor.TunePragmas;
                cmd.ExecuteNonQuery();
            }

            // WAL persists in the file header — a fresh connection reads it back.
            using var check = new SqliteConnection($"Data Source={path}");
            check.Open();
            using var q = check.CreateCommand();
            q.CommandText = "PRAGMA journal_mode;";
            ((string)q.ExecuteScalar()!).Should().Be("wal");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var f in new[] { path, path + "-wal", path + "-shm" })
                if (File.Exists(f)) File.Delete(f);
        }
    }

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
