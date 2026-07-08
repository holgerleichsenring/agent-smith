using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// Builder for PostPrCommentsContext. Repos[0] is the PR's repo — the
/// pr-event webhook seeds ContextKeys.SourceOverrideRepo so the run is
/// scoped to it (same transitional read as AnalyzePrDiffContextBuilder).
/// </summary>
public sealed class PostPrCommentsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
    {
        var repos = pipeline.Get<IReadOnlyList<RepoConnection>>(ContextKeys.Repos);
        return new PostPrCommentsContext(
            repos[0],
            pipeline.Get<string>(ContextKeys.PrNumber),
            pipeline.Get<PrReviewSummary>(ContextKeys.PrReviewSummary),
            pipeline);
    }
}
