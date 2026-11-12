using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ed: the whole-set cut review of a design turn's drafts — one reviewer over all of them, its
/// findings resolved to the look lines they cite. 2026-11-12-cd4ch: part of the one advisory pass. Null when the
/// call failed: a review that could not be taken blocks nothing.
/// </summary>
public sealed class ProposalWholeSetReview(
    ISpecCutReviewer reviewer, DerivationLookFactory looks, ILogger<ProposalWholeSetReview> logger)
{
    public async Task<IReadOnlyList<ProposalFinding>?> FindingsAsync(
        ProposalTurn turn, IReadOnlyList<SpecDraft> drafts, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(drafts);
        // The look owns none of the turn's repositories — the runner disposes them — so nothing is disposed.
        var look = looks.ForProposalReview(turn.Pipeline, turn.Agent);
        var key = turn.Pipeline.TryGet<string>(ContextKeys.DialogueJobId, out var jobId) && jobId is not null
            ? jobId
            : "spec-dialog";
        try
        {
            var review = await reviewer.ReviewAsync(drafts, key, ticketText: null, look, turn.Agent, turn.CostTracker, ct);
            return SpecDialogProposalFindings.Of(review, look?.Evidence.Looks);
        }
        // On the RUN's token, not on the exception's type: an LLM NetworkTimeout arrives as a
        // TaskCanceledException on an uncancelled token.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The whole-set review of this design turn's proposal could not be taken");
            return null;
        }
    }
}
