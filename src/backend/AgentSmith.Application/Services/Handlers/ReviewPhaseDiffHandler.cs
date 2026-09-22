using AgentSmith.Application.Models;
using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-17-042eh: after the verification, a fresh instance reads the phase's own diff
/// against the phase spec and the loaded principles, and what it finds gets ONE fix pass.
/// <para>
/// Nothing reviewed the code. VerifyPhase runs the build, the tests and the accountant, and
/// the accountant asks what the diff does not SATISFY of the done-list — a phase can pass all
/// of that while breaking every rule the repository states. The cut reviewer reads the plan
/// before code exists, and pr-review runs on a pull-request webhook, which a run never fires
/// for itself.
/// </para>
/// <para>
/// It never fails the phase. The phase verified; a finding is an improvement, and the one
/// consequence it earns is the fix pass — whose failure is reverted, not inherited.
/// </para>
/// </summary>
public sealed class ReviewPhaseDiffHandler(
    SandboxTargets sandboxTargets,
    PhaseDiffs phaseDiffs,
    DerivationLookFactory looks,
    PhaseDiffReviewer reviewer,
    PhaseReviewPublisher publisher,
    ILogger<ReviewPhaseDiffHandler> logger)
    : ICommandHandler<ReviewPhaseDiffContext>
{
    public async Task<CommandResult> ExecuteAsync(
        ReviewPhaseDiffContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<PhaseDraft>(ContextKeys.PhaseSpec, out var draft) || draft is null)
            return CommandResult.Ok("No phase is current; there is no diff to review.");

        // A fix pass that was reverted is NOT reviewed again: the branch carries the state the
        // first review already read, and asking would spend a call to be told the same thing.
        // The FIRST review's report stands — including its reason, if it was never taken.
        if (pipeline.TryGet<string>(ContextKeys.PhaseReviewReverted, out var reverted) && reverted is not null)
            return await RecordAsync(pipeline, draft, PhaseReviewLedger.ForThisPhase(pipeline),
                $"The fix pass was reverted, so the diff is the one already reviewed ({reverted}).",
                sandboxes: null, cancellationToken);

        var tracker = PipelineCostTracker.GetOrCreate(pipeline);
        if (tracker.IsBudgetExhausted)
            return await NotTakenAsync(
                pipeline, draft, CostCapStop.Describe(tracker), cancellationToken);
        if (!sandboxTargets.TryResolve(pipeline, out var sandboxes, out _))
            return await NotTakenAsync(
                pipeline, draft, "no sandbox was in the pipeline context", cancellationToken);

        var diffs = await phaseDiffs.TakeAsync(pipeline, sandboxes, cancellationToken);
        await using var look = looks.OnTerms(pipeline, DerivationLookTerms.PhaseReview);
        var principles = pipeline.TryGet<string>(ContextKeys.CodingPrinciples, out var p) ? p : null;
        var report = await reviewer.ReviewAsync(
            draft, principles, diffs, look, context.AgentConfig, tracker, cancellationToken);

        return await RecordAsync(
            pipeline, draft, report, Summary(report, diffs), sandboxes, cancellationToken);
    }

    /// <summary>
    /// The review did not happen, and the reason is RECORDED rather than left as an empty
    /// finding list — which every reader would render as "a fresh instance read this and
    /// objected to nothing".
    /// </summary>
    private Task<CommandResult> NotTakenAsync(
        PipelineContext pipeline, PhaseDraft draft, string why, CancellationToken ct) =>
        RecordAsync(
            pipeline, draft, PhaseReviewReport.NotTaken(why),
            $"The phase review was not taken: {why}.", sandboxes: null, ct);

    /// <summary>
    /// The findings reach three places and then decide whether a pass follows: the phase
    /// record's body, the phase_review artifact, and the run ledger the pull request reads.
    /// </summary>
    private async Task<CommandResult> RecordAsync(
        PipelineContext pipeline, PhaseDraft draft, PhaseReviewReport report,
        string message, IReadOnlyDictionary<string, ISandbox>? sandboxes, CancellationToken ct)
    {
        PhaseReviewLedger.Record(pipeline, report);
        await publisher.PublishAsync(pipeline, draft.PhaseId, report, ct);
        var decision = PhaseReviewFixPass.Decide(pipeline, report, sandboxes);
        var said = decision.Note is null ? message : $"{message} {decision.Note}.";
        logger.LogInformation("{Message}", said);
        return decision.Steps is { } steps
            ? CommandResult.OkAndContinueWith($"{message} One fix pass follows.", [.. steps])
            : CommandResult.Ok(said);
    }

    private static string Summary(PhaseReviewReport report, IReadOnlyList<PhaseDiff> diffs)
    {
        if (!report.Reviewed) return $"The phase review was {report.NotReviewedNote}.";
        var files = diffs.Sum(d => d.Paths.Count);
        return report.Findings.Count == 0
            ? $"The phase review read {files} changed file(s) and kept no finding."
            : $"The phase review kept {report.Findings.Count} finding(s) over {files} changed "
              + "file(s): "
              + string.Join("; ", report.Findings.Select(f => $"{f.Repository}/{f.Path}:{f.Line}"));
    }
}
