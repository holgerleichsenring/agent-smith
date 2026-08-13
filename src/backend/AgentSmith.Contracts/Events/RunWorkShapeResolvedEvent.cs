namespace AgentSmith.Contracts.Events;

/// <summary>
/// p0413: published by ScopeRepos when the classifier states the SHAPE of the
/// work (deterministic transformation / judgement / mixed) beside its size. The
/// shape decides how the ticket is cut, so it must be visible on the run — an
/// operator asking "why did this get three phases" reads the answer here.
/// <para>
/// Separate from <see cref="RunBudgetResolvedEvent"/> on purpose: an Unknown
/// complexity tier sizes no cap and publishes no budget, but a stated shape is
/// still a fact about the run.
/// </para>
/// </summary>
public sealed record RunWorkShapeResolvedEvent(
    string RunId,
    string Shape,
    string? Reason,
    DateTimeOffset Timestamp)
    : RunEvent(RunId, EventType.RunWorkShapeResolved, Timestamp);
