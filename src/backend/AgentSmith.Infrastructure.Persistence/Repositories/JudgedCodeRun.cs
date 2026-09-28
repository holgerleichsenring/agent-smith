using AgentSmith.Contracts.Runs;

namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// One finished coding run as the Criteria met figure reads it: its project, when it
/// finished, the acceptance snapshot it was judged by, and the operator's overrules of it.
/// </summary>
public sealed record JudgedCodeRun(
    string Project,
    DateTimeOffset FinishedAt,
    AcceptanceView? Acceptance,
    IReadOnlyList<CriterionOverrule> Overrules);
