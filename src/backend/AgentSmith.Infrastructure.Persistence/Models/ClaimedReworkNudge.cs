using AgentSmith.Contracts.Runs;

namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>2026-10-08-0781: a nudge this worker claimed — at the generation it claimed, by its token.</summary>
public sealed record ClaimedReworkNudge(
    string Project, string TicketId, ReworkNudgeOrigin Origin, string? PrUrl, string? Channel,
    long Generation, string ClaimToken, int Tries);
