using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-23-31dcb: what one project's series tickets are, read set-based — requires edges by dependent id, the
/// landed ticket ids, the pull-request records by ticket id, and each ticket's newest attempt.</summary>
public sealed record SeriesRunProjectFacts(
    ResolvedProject Project,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Requires,
    IReadOnlySet<string> Landed,
    ILookup<string, PrReviewRecordView> PullRequests,
    IReadOnlyDictionary<string, PreviousAttempt> Attempts);
