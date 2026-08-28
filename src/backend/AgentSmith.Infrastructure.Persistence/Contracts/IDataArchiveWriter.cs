using AgentSmith.Infrastructure.Persistence.Models;

namespace AgentSmith.Infrastructure.Persistence.Contracts;

/// <summary>
/// 2026-08-28-2af6: writes every table of a store into one archive — a manifest first,
/// then one line-delimited JSON file per table. Data, never SQL text: the archive exists
/// to cross providers, and SQL is bound to the dialect that produced it.
/// </summary>
public interface IDataArchiveWriter
{
    /// <summary>
    /// Writes the whole store into <paramref name="destination"/> as a zip and returns the
    /// manifest it wrote. The read runs in one transaction, so the counts in the manifest
    /// and the rows behind them are the same instant.
    /// </summary>
    Task<DataArchiveManifest> WriteAsync(
        AgentSmithDbContext db, Stream destination, CancellationToken cancellationToken = default);
}
