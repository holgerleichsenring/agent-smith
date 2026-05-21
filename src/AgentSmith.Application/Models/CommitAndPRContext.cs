using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for committing changes and creating a pull request across the
/// run's repos. Configs is the full list; the handler iterates per-repo.
/// The primary Repository (Configs[0]'s checkout result) is carried for
/// legacy single-repo skill semantics.
/// </summary>
public sealed record CommitAndPRContext(
    Repository Repository,
    IReadOnlyList<CodeChange> Changes,
    Ticket Ticket,
    IReadOnlyList<RepoConnection> Configs,
    TrackerConnection TrackerConnection,
    PipelineContext Pipeline) : ICommandContext
{
    public RepoConnection RepoConnection => Configs[0];
}
