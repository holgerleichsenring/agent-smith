using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-023fb: the run's repository and branch, and the pull request and base the server seeded.</summary>
public sealed record RefreshBranchBaseContext(
    IReadOnlyList<RepoConnection> Repos, string? Branch, string? PrUrl, string? PrNumber, string? BaseBranch,
    PipelineContext Pipeline) : ICommandContext;
