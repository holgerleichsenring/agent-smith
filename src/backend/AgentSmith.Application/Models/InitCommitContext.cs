using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for committing .agentsmith/ files and creating a PR during project
/// init across the run's repos. Configs is the full list; the handler
/// iterates per-repo. Unlike CommitAndPRContext, no ticket is needed.
/// </summary>
public sealed record InitCommitContext(
    Repository Repository,
    IReadOnlyList<RepoConnection> Configs,
    TrackerConnection TrackerConnection,
    PipelineContext Pipeline) : ICommandContext
{
    public RepoConnection RepoConnection => Configs[0];
}
