using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863f: what one deploy delivered — the version, the repository it went to, the link to follow,
/// and the pull request or the pipeline run on the host (neither for a run the host did not name).</summary>
public sealed record DeployDelivered(
    string Version, RepoConnection Repo, string Url, string? PullRequestUrl = null, string? PipelineRunId = null);
