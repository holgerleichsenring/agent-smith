using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Interceptors;

/// <summary>
/// SQLite is a single file under concurrent server access (poller, SignalR
/// broadcaster, request handlers, background reapers). Out of the box that
/// produces sporadic "database is locked" failures, which EF surfaces as the
/// opaque <c>Database.Connection</c> event "An error occurred using the
/// connection to database '{db}' on server '{file}'" — with no exception detail,
/// so the cause is invisible.
/// <para>
/// On every physical open we set WAL journaling (readers no longer block on a
/// writer) + a busy timeout (a contending writer waits instead of failing
/// instantly), which removes the contention errors at the source. journal_mode
/// persists in the file header; busy_timeout is per-connection, so both are
/// (re)applied per open. On a connection failure we log the REAL exception with
/// its SQLite error code, so any other cause (permissions, disk full, corrupt
/// file) is never hidden again.
/// </para>
/// </summary>
public sealed class SqliteTuningInterceptor(ILogger<SqliteTuningInterceptor> logger)
    : DbConnectionInterceptor
{
    private const int BusyTimeoutMs = 5000;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
        => Tune(connection);

    public override Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken)
    {
        Tune(connection);
        return Task.CompletedTask;
    }

    public override void ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData)
        => LogFailure(connection, eventData.Exception);

    public override Task ConnectionFailedAsync(
        DbConnection connection, ConnectionErrorEventData eventData, CancellationToken cancellationToken)
    {
        LogFailure(connection, eventData.Exception);
        return Task.CompletedTask;
    }

    // p0348: busy_timeout FIRST, journal_mode second. The first connection to a
    // file the migrate step left in DELETE mode must convert it to WAL, which
    // needs an exclusive lock — if busy_timeout were still 0 at that moment a
    // concurrent boot connection (poller, reaper, first request) would get an
    // instant SQLITE_BUSY ("database is locked") instead of waiting. Setting the
    // timeout first makes the lock-contended conversion wait out the grace.
    // p0348: public so the migrate one-shot (DatabaseCommand) applies the SAME
    // tuning — it must leave the file in WAL, otherwise the FIRST server
    // connection performs the DELETE->WAL conversion under boot contention.
    public static readonly string TunePragmas =
        $"PRAGMA busy_timeout={BusyTimeoutMs}; PRAGMA journal_mode=WAL;";

    private static void Tune(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = TunePragmas;
        cmd.ExecuteNonQuery();
    }

    internal void LogFailure(DbConnection connection, Exception exception)
    {
        // A cancelled open (aborted request, shutdown) is expected, not a failure — don't
        // shout FAIL with a stack trace. Real errors (lock, permissions, disk) stay at Error.
        if (exception is OperationCanceledException)
        {
            logger.LogDebug(
                "SQLite connection open canceled (dataSource={DataSource}) — caller aborted, not a failure",
                connection.DataSource);
            return;
        }

        var code = exception is SqliteException sqlite ? sqlite.SqliteErrorCode.ToString() : "n/a";
        logger.LogError(exception,
            "SQLite connection failed (dataSource={DataSource}, sqliteErrorCode={Code}): {Message}",
            connection.DataSource, code, exception.Message);
    }
}
