namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0357: published by ScopeRepos when the complexity classifier sizes the run's
/// cost cap ("tier large — $45 / 15,000,000 tokens"). Until now the resolved cap
/// existed only as a log string, so the run page showed spent-with-no-denominator.
/// The applier persists tier + cap onto the run row; the detail snapshot serves
/// them and the dashboard renders a spent/cap budget bar from step 4 onward.
/// </summary>
public sealed record RunBudgetResolvedEvent(
    string RunId,
    string Tier,
    decimal CapUsd,
    long CapTokens,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.RunBudgetResolved, Timestamp);
