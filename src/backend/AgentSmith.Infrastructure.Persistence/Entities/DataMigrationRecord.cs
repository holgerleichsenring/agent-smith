namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-11-22-747da: a data migration this store has had — run once by `database migrate`, or proven already run by
/// the schema migration it shipped with. Its presence is the whole fact: a recorded id is never run again.
/// </summary>
public sealed class DataMigrationRecord
{
    /// <summary>The data migration's id, e.g. <c>2026-10-12-967ab-spec-names</c>.</summary>
    public string Id { get; set; } = string.Empty;

    public DateTimeOffset AppliedAt { get; set; }

    /// <summary>True when the store's applied schema migrations proved it ran before this ledger existed.</summary>
    public bool Proven { get; set; }
}
