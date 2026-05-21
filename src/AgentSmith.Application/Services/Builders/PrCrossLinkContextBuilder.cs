using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// Builder for PrCrossLinkContext. Carries the run's Repos so the handler can
/// resolve a per-repo ISourceProvider when issuing the body PATCH.
/// </summary>
public sealed class PrCrossLinkContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
        => new PrCrossLinkContext(
            pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos),
            pipeline);
}
