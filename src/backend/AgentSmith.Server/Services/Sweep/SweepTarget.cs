using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>2026-10-08-10b0: a repository the PR sweep reads, with the config and the polling entry's
/// projects its routes are limited to.</summary>
public sealed record SweepTarget(AgentSmithConfig Config, RepoConnection Repo, IReadOnlySet<string> Allowed);
