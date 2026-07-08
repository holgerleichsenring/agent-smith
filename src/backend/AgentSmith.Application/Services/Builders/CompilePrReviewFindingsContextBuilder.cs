using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// Builder for CompilePrReviewFindingsContext. Requires the structured PR
/// diff (AnalyzePrDiff output) — inline anchors are validated against its
/// new-side lines; observations are read from the pipeline by the handler
/// (a review round may legitimately produce none).
/// </summary>
public sealed class CompilePrReviewFindingsContextBuilder : IContextBuilder
{
    public ICommandContext Build(PipelineCommand command, ResolvedProject project, PipelineContext pipeline)
        => new CompilePrReviewFindingsContext(
            pipeline.Get<PrDiffAnalysis>(ContextKeys.PrDiff),
            pipeline);
}
