namespace AgentSmith.Contracts.Memory;

/// <summary>
/// 2026-10-25-9324a: one fact about a memory's use, kept per repository and slug. Run, spec, project and ticket
/// are plain values, not references: the fact outlives the run it came from. <see cref="EventSeq"/> is the trail
/// position of the event that wrote it, so a replayed event is written once; <see cref="CriterionKey"/> names the
/// judged criterion of a person's contradiction, so withdrawing that judgement retracts exactly its rows.
/// </summary>
public sealed record MemoryEvidenceFact(
    string RepoKey,
    string Slug,
    string Kind,
    double Weight,
    string Project,
    string TicketId,
    string RunId,
    string SpecId,
    string? Reason,
    DateTimeOffset At,
    long? EventSeq = null,
    string? CriterionKey = null);
