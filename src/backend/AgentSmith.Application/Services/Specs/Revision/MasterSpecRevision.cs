using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs.Revision;

/// <summary>
/// 2026-11-17-ec48a: the coding master revises the spec it is building — what an operator's message or a corrected
/// premise asks for — instead of pausing the run. The gate admits it, the text is read, ONE review decides
/// (<see cref="SpecRevisionReview"/>), a cleared text is written and becomes the spec's criteria
/// (<see cref="SpecRevisionWriter"/>); anything else changes nothing and the reply says why. With spec_review.rounds 0
/// the review is off, as it is for the pre-build step, and the text is written unreviewed. The review is bounded by the
/// run's cost cap and wall time; each one is a round of its own after the pre-build rounds.
/// </summary>
public sealed class MasterSpecRevision(
    SpecRevisionGate gate, SpecRevisionText texts, ISpecReviewSettingsReader settings, SpecRevisionReview review,
    SpecRevisionWriter writer)
{
    public async Task<string> ReviseAsync(
        PipelineContext pipeline, AgentConfig agent, string? yaml, string? reason, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (string.IsNullOrWhiteSpace(reason)) return SpecRevisionReplies.NotRevised("give the reason for the revision");
        if (await gate.RefusalAsync(pipeline, ct) is { } refused) return SpecRevisionReplies.NotRevised(refused);
        var current = pipeline.Get<SpecDraft>(ContextKeys.CurrentSpec);
        var (draft, refusal) = texts.Read(current, yaml, pipeline.Get<SpecSet>(ContextKeys.SpecSet).IsStandIn(current.SpecId));
        if (draft is null) return SpecRevisionReplies.NotRevised(refusal!);
        var (run, stop) = Run(pipeline, agent, reason);
        if (run is null) return SpecRevisionReplies.NotRevised(stop!);
        var round = NextRound(run);
        if (run.Settings.Review.Rounds == 0) return await writer.WriteAsync(run, draft, reason, null, round, ct);
        var verdict = await review.ReviewAsync(run, draft, round,
            CheckpointedValues.Strings(pipeline, ContextKeys.TicketMessagesHanded), ct);
        return verdict.IsCleared
            ? await writer.WriteAsync(run, verdict.Text, reason, verdict, round, ct)
            : await NotClearedAsync(verdict, draft.SpecId, round, ct);
    }

    private async Task<string> NotClearedAsync(SpecRevisionVerdict verdict, string specId, int round, CancellationToken ct)
    {
        await writer.RoundAsync(specId, verdict, round, null, ct);
        return verdict.Outcome switch
        {
            SpecRevisionOutcome.Refused => SpecRevisionReplies.Refused(verdict),
            SpecRevisionOutcome.Unresolved => SpecRevisionReplies.Unresolved(verdict),
            SpecRevisionOutcome.CutsNotApplied => SpecRevisionReplies.CutsNotApplied(verdict),
            _ => SpecRevisionReplies.NotTaken(),
        };
    }

    private (SpecReviewRun? Run, string? Stop) Run(PipelineContext pipeline, AgentConfig agent, string reason)
    {
        var (current, invalid) = Settings();
        if (current is null) return (null, invalid);
        if (SpecReviewBudget.Exhausted(pipeline, current) is { } exhausted) return (null, exhausted);
        var set = pipeline.Get<SpecSet>(ContextKeys.SpecSet);
        var ticket = pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var t) ? t : null;
        var wider = string.Join("\n\n", new[] { SpecReviewWiderGoal.Of(ticket, set),
            $"The agent building this spec revised it while working, because: {reason.Trim()}" }.Where(p => p is not null));
        return (new SpecReviewRun(pipeline, current, agent, wider), null);
    }

    private (SpecReviewSettings? Settings, string? Invalid) Settings()
    {
        try
        {
            var current = settings.Current();
            return current.Review.Problems() is { Count: > 0 } problems
                ? (null, $"the spec_review settings are invalid: {string.Join("; ", problems)}")
                : (current, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, $"the spec_review settings could not be read: {ex.Message}");
        }
    }

    // Each revision review is stored as a round of its own, after every round the pre-build review may have held.
    private static int NextRound(SpecReviewRun run)
    {
        var held = CheckpointedValues.Int(run.Pipeline, ContextKeys.SpecRevisionReviews, 0) + 1;
        run.Pipeline.Set(ContextKeys.SpecRevisionReviews, held);
        return run.Settings.Review.Rounds + held;
    }
}
