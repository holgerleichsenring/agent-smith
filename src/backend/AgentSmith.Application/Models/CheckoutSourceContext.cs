using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for checking out the run's source repos. Configs is the full list
/// of configured repos for the run; multi-repo handlers iterate, single-repo
/// handlers can read Config (the computed primary = Configs[0]).
/// p0496: Branch carries where the name came from, not just what it is.
/// </summary>
public sealed record CheckoutSourceContext(
    IReadOnlyList<RepoConnection> Configs,
    RunBranch? Branch,
    PipelineContext Pipeline) : ICommandContext
{
    public RepoConnection Config => Configs[0];
}
