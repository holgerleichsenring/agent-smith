using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for the PersistWorkBranchCommand — runs in the pipeline's failure
/// path when local changes exist that would otherwise be lost with the
/// container's /tmp. Configs is the full list of run repos; the handler
/// attempts the WIP push per repo and aggregates outcomes.
/// </summary>
public sealed record PersistWorkBranchContext(
    IReadOnlyList<RepoConnection> Configs,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext
{
    public RepoConnection Source => Configs[0];
}
