using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for checking out the run's source repos. Configs is the full list
/// of configured repos for the run; multi-repo handlers iterate, single-repo
/// handlers can read Config (the computed primary = Configs[0]).
/// </summary>
public sealed record CheckoutSourceContext(
    IReadOnlyList<RepoConnection> Configs,
    BranchName? Branch,
    PipelineContext Pipeline) : ICommandContext
{
    public RepoConnection Config => Configs[0];
}
