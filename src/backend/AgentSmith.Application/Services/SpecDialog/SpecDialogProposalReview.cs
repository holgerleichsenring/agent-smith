using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Turns;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-17-042ed: reviews a design turn's phase or epic proposal INSIDE the turn, with a fresh
/// instance reading the turn's own repositories.
/// <para>
/// Inside the turn because the read-only scopes are disposed in the runner's finally, so a review
/// taken later would have nothing to read. It runs where the outcome is gated and hands the
/// findings back on the proposal itself. A review that could not be taken blocks nothing: it is
/// not evidence of a fault, and an operator is never stopped from approving by a model call that
/// failed.
/// </para>
/// <para>
/// Its own type, not a branch in the handler: 2026-09-17-042ee reports "reviewing" and the look's
/// reads from here.
/// </para>
/// <para>
/// 2026-10-02-3f06c: the evidence check runs first and each half in its own guard, so a model
/// call that fails keeps what the check found, and one write carries both.
/// </para>
/// </summary>
public sealed class SpecDialogProposalReview(
    ISpecCutReviewer reviewer,
    DerivationLookFactory looks,
    ProposalEvidenceReview evidence,
    ITurnActivityObserverAccessor turnActivity, // 2026-09-17-042ee: the review is not dark
    ILogger<SpecDialogProposalReview> logger)
{
    public async Task ReviewAsync(
        PipelineContext pipeline, AgentConfig agent, PipelineCostTracker costTracker, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<OutcomeProposal>(ContextKeys.SpecDialogOutcome, out var proposal)
            || proposal is null || DraftsOf(proposal) is not { Count: > 0 } drafts) return;

        await turnActivity.ReportAsync(new TurnActivity(TurnActivityKind.Reviewing), ct);
        var findings = new List<ProposalFinding>();
        try
        {
            findings.AddRange(await evidence.FindingsAsync(pipeline, proposal, ct));
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The evidence of this design turn's proposal could not be checked");
        }
        findings.AddRange(await ModelFindingsAsync(pipeline, drafts, agent, costTracker, ct));
        if (findings.Count == 0) return;
        logger.LogInformation(
            "The review of this design turn's proposal reported {Count} finding(s)", findings.Count);
        pipeline.Set(ContextKeys.SpecDialogOutcome, proposal with { Findings = findings });
    }

    private async Task<IReadOnlyList<ProposalFinding>> ModelFindingsAsync(
        PipelineContext pipeline, IReadOnlyList<PhaseDraft> drafts, AgentConfig agent,
        PipelineCostTracker costTracker, CancellationToken ct)
    {
        // The look is over the turn's repositories and owns none of them — the runner disposes
        // the scopes it opened — so there is nothing to dispose here. Its tools report each
        // read and search through the same observer the review reported on.
        var look = looks.ForProposalReview(pipeline);
        var key = pipeline.TryGet<string>(ContextKeys.DialogueJobId, out var jobId) && jobId is not null
            ? jobId
            : "spec-dialog";
        try
        {
            var review = await reviewer.ReviewAsync(drafts, key, ticketText: null, look, agent, costTracker, ct);
            return SpecDialogProposalFindings.Of(review, look?.Evidence.Looks);
        }
        // On the RUN's token, not on the exception's type: an LLM NetworkTimeout arrives as a
        // TaskCanceledException on an uncancelled token, and letting it escape here would destroy
        // the reply this turn already has — the call sits outside the master handler's own guard.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The review of this design turn's proposal could not be taken");
            return [];
        }
    }

    /// <summary>The phases a proposal states. An answer states none, and a bug is not a plan —
    /// neither is reviewed against the code.</summary>
    private static IReadOnlyList<PhaseDraft> DraftsOf(OutcomeProposal proposal) => proposal switch
    {
        PhaseOutcome phase => [phase.Draft],
        EpicOutcome epic => [epic.Parent, .. epic.Children],
        _ => [],
    };
}
