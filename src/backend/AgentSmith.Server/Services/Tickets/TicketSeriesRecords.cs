using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59e: the rows a series read joins, for a set of attempts — the cuts in publish order,
/// the RunSpec standings, the attempt records, and (for the full read) each attempt's spec-gate step
/// status and operator judgements; the strip asks only which attempts hold a checkpoint.
/// </summary>
public sealed record TicketSeriesRecords(
    IReadOnlyList<RunSeriesCut> Cuts,
    IReadOnlyList<RunSpec> Specs,
    IReadOnlyDictionary<string, RunAttempt> Attempts,
    IReadOnlyDictionary<string, string> GateSteps,
    IReadOnlyList<RunCriterionJudgement> Judgements,
    IReadOnlySet<string> Checkpointed);
