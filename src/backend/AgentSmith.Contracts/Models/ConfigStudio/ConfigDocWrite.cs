namespace AgentSmith.Contracts.Models.ConfigStudio;

/// <summary>
/// p0349: a single transactional write to the entity-document store — the doc row
/// plus its derived reference edges, committed together.
/// <see cref="ExpectedVersion"/> drives optimistic concurrency (null = a fresh
/// insert, no prior version to match); a mismatch is a 409.
/// </summary>
public sealed record ConfigDocWrite(
    string Type,
    string Id,
    string Doc,
    int? ExpectedVersion,
    IReadOnlyList<ConfigDocEdge> Edges,
    string ChangedBy,
    string? Note = null);
