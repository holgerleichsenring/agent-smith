namespace AgentSmith.Infrastructure.Persistence.Services.DataMigrations;

/// <summary>
/// 2026-11-22-747da: whether a store had reached a schema migration. Every migration set names its migrations
/// <c>&lt;14-digit UTC timestamp&gt;_&lt;Name&gt;</c>, so comparing the timestamp prefix orders all three sets alike — a
/// SQL Server id a few seconds later than the shared set's twin, and the PostgreSQL set, which starts after both.
/// </summary>
public sealed class SchemaWatermark
{
    public const int TimestampLength = 14; // not a limit: the width of the timestamp every migration id starts with

    /// <summary>True when an id in <paramref name="applied"/> is at or after <paramref name="timestamp"/>.</summary>
    public bool Reached(IEnumerable<string> applied, string? timestamp)
    {
        ArgumentNullException.ThrowIfNull(applied);
        return timestamp is not null && applied.Any(id =>
            id.Length >= TimestampLength && string.CompareOrdinal(id[..TimestampLength], timestamp) >= 0);
    }
}
