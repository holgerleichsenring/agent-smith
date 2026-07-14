namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// p0329: one ratification outcome joined to its run's project — the raw row
/// the expectation-metrics aggregation runs over. Outcome vocabulary is
/// <c>ExpectationOutcomes</c> (verbatim/edited/rejected/unratified).
/// </summary>
public sealed record ExpectationOutcomeRow(
    string Project, string Outcome, int EditDistance, DateTimeOffset RatifiedAt);
