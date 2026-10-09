namespace AgentSmith.Contracts.Sweep;

/// <summary>
/// 2026-10-08-9e6e: one thing that changed since a cursor — a ticket (TicketId), or a pull request
/// (PrUrl, its head branch, whether the head lives in the same repository) — with the time the
/// cursor moves to. <see cref="Dedupe"/> names a change a source cannot time (an Azure DevOps vote),
/// so the sweep nudges it once.
/// </summary>
public sealed record ChangedItem(
    DateTimeOffset At, string? TicketId = null, string? PrUrl = null, string? HeadRef = null,
    bool SameRepository = true, string? Dedupe = null);
