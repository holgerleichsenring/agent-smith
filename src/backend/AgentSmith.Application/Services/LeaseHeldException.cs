namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-10-08-e8b9e: a run that found another live run holding its ticket's lease. It ends before it
/// starts: a reserved run row is marked failed by the prologue's rule, a resume publishes nothing and
/// stays parked for the next tick, and the holder's lease, ticket and taken-ticket record are untouched.
/// </summary>
public sealed class LeaseHeldException(string ticketId, string? holderRunId, bool resume)
    : InvalidOperationException($"Ticket {ticketId} is held by run {holderRunId ?? "(starting)"} — this run did not start.")
{
    public string? HolderRunId { get; } = holderRunId;

    public bool Resume { get; } = resume;
}
