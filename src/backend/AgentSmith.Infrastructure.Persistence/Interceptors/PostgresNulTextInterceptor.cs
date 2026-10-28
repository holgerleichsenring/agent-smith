using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AgentSmith.Infrastructure.Persistence.Interceptors;

/// <summary>
/// 2026-10-28-ebb0: PostgreSQL's text types cannot hold a NUL character — a write carrying one
/// fails with "invalid byte sequence for encoding UTF8: 0x00", where SQLite and SQL Server store
/// it. Tool output, run events and transcripts are text an agent did not choose, so on this
/// provider a single NUL in a command's stdout would fail the write that records it. Every string
/// parameter therefore has its NULs replaced with U+FFFD before the command is sent: the row is
/// stored, and the replacement character marks where the store could not keep what it was given.
/// <para>
/// A command interceptor rather than a value converter, so no column, no LINQ translation and no
/// model snapshot is touched; registered by <c>UseProvider</c> for PostgreSQL only.
/// </para>
/// </summary>
public sealed class PostgresNulTextInterceptor : DbCommandInterceptor
{
    public const char Replacement = '�';

    public static readonly PostgresNulTextInterceptor Instance = new();

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Scrub(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Scrub(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Scrub(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Scrub(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Scrub(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Scrub(command);
        return ValueTask.FromResult(result);
    }

    /// <summary>Replaces every NUL in the command's string parameters.</summary>
    public static void Scrub(DbCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        foreach (DbParameter parameter in command.Parameters)
            if (parameter.Value is string text && text.Contains('\0'))
                parameter.Value = text.Replace('\0', Replacement);
    }
}
