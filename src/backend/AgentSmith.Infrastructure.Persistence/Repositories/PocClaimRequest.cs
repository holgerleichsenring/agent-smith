namespace AgentSmith.Infrastructure.Persistence.Repositories;

/// <summary>
/// 2026-11-23-933db: one claim of a PoC's run slot — the run id minted for it, the queued row written with it, the
/// revision it takes in (stamped with the run id), and the run id this same request just awaited to completion, which
/// the claim may replace (the handover: the run row is finished only later by the event drain).
/// </summary>
public sealed record PocClaimRequest(
    long PocId, string RunId, string Project, string Pipeline, IReadOnlyList<string> Repos, string Summary,
    TimeSpan StaleAfter, long? RevisionId = null, string? HandoverRunId = null);
