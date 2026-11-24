namespace AgentSmith.Application.Services.Pocs.Rollout;

/// <summary>2026-11-23-c81ec: a finished follow — the PoC, the revision it rolled out, what it waited for, the last
/// reading and the budget that held it, and the PoC's host for the settled sentence.</summary>
public sealed record PocRolloutEnd(
    long PocId, string Name, int Revision, string? Host, PocRolloutExpected Expected, PocRolloutReading Reading,
    PocRolloutBudget Budget);
