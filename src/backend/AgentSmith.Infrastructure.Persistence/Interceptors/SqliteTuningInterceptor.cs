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

    private static void Tune(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA journal_mode=WAL; PRAGMA busy_timeout={BusyTimeoutMs};";
        cmd.ExecuteNonQuery();
    }

    private void LogFailure(DbConnection connection, Exception exception)
    {
        var code = exception is SqliteException sqlite ? sqlite.SqliteErrorCode.ToString() : "n/a";
        logger.LogError(exception,
            "SQLite connection failed (dataSource={DataSource}, sqliteErrorCode={Code}): {Message}",
            connection.DataSource, code, exception.Message);
    }
}
