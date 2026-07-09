using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0167c: compiles the pr-review skill observations into the typed
/// <see cref="PrReviewSummary"/> behind ContextKeys.PrReviewSummary. The
/// selector applies the render budget (group by file + line range, most
/// severe first, cap at 25 inline); the renderer produces the markdown
/// bodies. Zero observations still compile — the summary then reports a
/// clean review, so PostPrComments always has something to post.
/// </summary>
public sealed class CompilePrReviewFindingsHandler(
    PrReviewFindingSelector selector,
    PrReviewCommentRenderer renderer,
    ILogger<CompilePrReviewFindingsHandler> logger)
    : ICommandHandler<CompilePrReviewFindingsContext>
{
    public Task<CommandResult> ExecuteAsync(
        CompilePrReviewFindingsContext context, CancellationToken cancellationToken)
    {
        var runId = context.Pipeline.Get<string>(ContextKeys.RunId);
        context.Pipeline.TryGet<List<SkillObservation>>(
            ContextKeys.SkillObservations, out var observations);

        var selection = selector.Select(observations ?? [], context.Diff);
        var inline = selection.Inline.Select(ToInlineComment).ToList();
        var summary = new PrReviewSummary(renderer.RenderSummary(selection, runId), inline);
        context.Pipeline.Set(ContextKeys.PrReviewSummary, summary);

        var message = $"Compiled {selection.TotalFindings} finding(s) into "
            + $"{inline.Count} inline comment(s) + summary "
            + $"({selection.SummaryOnly.Count} folded into the summary)";
        logger.LogInformation("{Message}", message);
        return Task.FromResult(CommandResult.Ok(message));
    }

    private PrReviewInlineComment ToInlineComment(PrReviewFindingGroup group) => new(
        group.File,
        group.StartLine,
        group.EndLine,
        group.TopSeverity.ToString(),
        group.Observations[0].Category,
        renderer.RenderInline(group));
}
