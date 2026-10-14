using AgentSmith.Contracts.Events;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762e: what one review round says about itself, beside its reviewers' answers. The evidence
/// problems and residues are the round's; <see cref="SpecId"/> is null on a series round.
/// </summary>
public sealed record SpecReviewRound(
    string? SpecId,
    string Scope,
    int Round,
    IReadOnlyList<SpecEvidenceProblem> EvidenceProblems,
    IReadOnlyList<SpecReviewResidue> Residues,
    int? RevisionNumber = null);

/// <summary>
/// 2026-10-13-8762e: stores a round as one <see cref="SpecReviewRoundEvent"/> per reviewer answer, after
/// its caller applied the cuts. There is no bundled round type: the caller runs the evidence check once,
/// the reviewers, the cuts, then this. Nothing is published outside a run, and a failed publish is logged,
/// never thrown — a review is never worth a run.
/// </summary>
public sealed class SpecReviewRoundPublisher(
    IEventPublisher events, IRunContextAccessor runContext, ILogger<SpecReviewRoundPublisher> logger)
{
    /// <summary>Publishes one event per answer; a null answer is a reviewer not taken and publishes
    /// nothing. The reviewer index is the answer's position.</summary>
    public async Task PublishAsync(
        SpecReviewRound round, IReadOnlyList<SpecAttackResult?> answers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(round);
        ArgumentNullException.ThrowIfNull(answers);
        var runId = runContext.CurrentRunId;
        if (string.IsNullOrEmpty(runId)) return;
        for (var reviewer = 0; reviewer < answers.Count; reviewer++)
        {
            if (answers[reviewer] is not { } answer) continue;
            try
            {
                await events.PublishAsync(new SpecReviewRoundEvent(
                    runId, DateTimeOffset.UtcNow, round.SpecId, round.Scope, round.Round, reviewer,
                    answer.Verdict, answer.Cuts, answer.Findings, round.EvidenceProblems, round.Residues,
                    round.RevisionNumber), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Review round {Round} of {Spec} in run {RunId} was not published",
                    round.Round, round.SpecId ?? round.Scope, runId);
            }
        }
    }
}
