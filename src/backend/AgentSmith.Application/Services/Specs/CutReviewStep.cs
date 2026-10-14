using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-9a59g: the review step of the derivation loop. A fresh instance checks whether a parsed cut
/// can be DELIVERED (p0422); a refused cut is offered to the least-objected set the loop falls back to and
/// answered with the rejection the deriver is handed back. Every review is published as
/// <see cref="SpecCutReviewedEvent"/> — accepted ones too, since an accepted cut is a turn of the ticket's
/// conversation — and nothing is published outside a run.
/// </summary>
public sealed class CutReviewStep(
    ISpecCutReviewer reviewer, IEventPublisher events, IRunContextAccessor runContext, ILogger<CutReviewStep> logger)
{
    /// <summary>Reviews the cut of <paramref name="attempt"/>; returns the rejection, or null when it is deliverable.</summary>
    internal async Task<string?> ReviewAsync(
        SpecDerivation cut, Ticket ticket, int attempt, LeastObjectedCut kept, DerivationLookFactory looks,
        AgentConfig agentConfig, PipelineContext pipeline, CancellationToken cancellationToken)
    {
        // 2026-09-15-ffa7: the reviewer keeps nothing between calls, so its look is per attempt.
        await using var look = looks.ForCutReview(pipeline);
        var review = await reviewer.ReviewAsync(
            [.. cut.Set.Specs.Select(p => p.Draft)], cut.Set.Key, SpecCutReviewTicketText.Of(ticket), look,
            agentConfig, PipelineCostTracker.GetOrCreate(pipeline), cancellationToken);
        await PublishAsync(attempt, review, cancellationToken);
        if (review.Deliverable) return null;
        kept.Offer(cut, review);
        var rejection = SpecCutRejection.For(review, look?.Evidence.Lines);
        logger.LogWarning("Spec cut attempt {Attempt} is not deliverable: {Error}", attempt, rejection);
        return rejection;
    }

    private async Task PublishAsync(int attempt, SpecCutReview review, CancellationToken cancellationToken)
    {
        var runId = runContext.CurrentRunId;
        if (string.IsNullOrEmpty(runId)) return;
        var deliverable = review.Deliverable ? CutReviewDeliverables.Accepted : CutReviewDeliverables.Refused;
        try
        {
            await events.PublishAsync(new SpecCutReviewedEvent(
                runId, DateTimeOffset.UtcNow, attempt, deliverable, review.Findings), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "The cut review of run {RunId} was not published", runId);
        }
    }
}
