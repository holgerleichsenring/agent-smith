using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for compiling the pr-review skill observations into a
/// <see cref="PrReviewSummary"/>. Diff is the structured PR diff published by
/// AnalyzePrDiff — the compiler validates inline anchors against it so only
/// lines the platform can attach a review comment to go inline.
/// </summary>
public sealed record CompilePrReviewFindingsContext(
    PrDiffAnalysis Diff,
    PipelineContext Pipeline) : ICommandContext;
