using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Models;

/// <summary>
/// The acceptance one run recorded that a run-record read cannot tie to a single spec — criteria no
/// spec's done list names, and the accounts a run with more than one spec could not take.
/// </summary>
public sealed record TicketSeriesUnattributedView(
    string RunId,
    int Attempt,
    IReadOnlyList<TicketSeriesCriterionView> Criteria,
    IReadOnlyList<UnmeasuredAccountView> Unmeasured);
