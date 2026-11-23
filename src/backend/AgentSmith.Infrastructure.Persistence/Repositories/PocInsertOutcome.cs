namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>2026-11-23-933db: what inserting a new Poc row came to.</summary>
public enum PocInsertOutcome
{
    Inserted,

    /// <summary>A live PoC on the target holds the name — a unique-index violation from a concurrent import included.</summary>
    NameTaken,

    /// <summary>The owner's live PoCs on the target would pass the per-person limit; the insert was rolled back.</summary>
    OverLimit,
}
