namespace AgentSmith.Server.Services.Pocs.Rollouts;

/// <summary>2026-11-23-c81ec: GET /api/pocs/{pocId}/rollouts/{runId} — the Rollout page's PoC facts beside the run's own
/// steps: the revision the run rolled out, the PoC state, how its rollout ended (null while it follows), the PoC's URL
/// once it settled healthy, the host build's run page and both commits, the GitOps one linked when the source repo has
/// a web URL.</summary>
internal sealed record PocRolloutAnswer(
    long PocId, string Name, string RunId, int? Revision, string State, string? RolloutState, DateTimeOffset? RolloutAt,
    string? Url, string? PipelineUrl, string? SourceCommitSha, string? GitopsCommitSha, string? GitopsCommitUrl);
