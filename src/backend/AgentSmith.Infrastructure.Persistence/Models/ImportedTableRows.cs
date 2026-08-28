namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>
/// 2026-08-28-2af6: the outcome of writing one table's rows out of an archive — how many
/// arrived, and the largest generated key among them, which is what the provider's
/// identity generator has to be advanced past.
/// </summary>
/// <param name="Rows">The number of rows written.</param>
/// <param name="MaxKey">The largest integer primary key written, or 0 when the table has none.</param>
public sealed record ImportedTableRows(long Rows, long MaxKey);
