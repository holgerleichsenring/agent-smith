using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-17-ec48a: ONE review of a spec text the coding master revised, the way the spec-first review-spec reviews a
/// spec: the cited facts are resolved first (an unresolved one asks no reviewer), then one round of the run's own
/// panel under spec_review, merged, and on BUILD WITH CUTS the cuts applied by the run's own reviser. A cut text is
/// settled, not attacked again. The reviewers read the master's reason in the wider goal and the operator messages the
/// run was handed as the owner's words.
/// </summary>
public sealed class SpecRevisionReview(SpecReviewPanel panel, ISpecRoundEvidence evidence, ISpecCutReviser reviser)
{
    public async Task<SpecRevisionVerdict> ReviewAsync(
        SpecReviewRun run, SpecDraft text, int round, IReadOnlyList<string> operatorTurns, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(operatorTurns);
        var seen = await evidence.CheckAsync(run.Pipeline, text, run.Pipeline.Get<SpecSet>(ContextKeys.SpecSet), ct);
        if (seen.Problems.Count > 0) return new(SpecRevisionOutcome.Unresolved, text, null, [], seen);
        var input = SpecReviewInput.Of(run, text, round, seen, []) with { OperatorTurns = operatorTurns };
        var answers = await panel.ReviewAsync(run.Pipeline, input, run.Settings.Review, run.Agent, ct);
        var merged = SpecReviewMerge.Merge(answers);
        if (!merged.IsTaken) return new(SpecRevisionOutcome.NotTaken, text, merged, answers, seen);
        return merged.Verdict == SpecReviewVerdicts.Refuse
            ? new(SpecRevisionOutcome.Refused, text, merged, answers, seen)
            : await ApplyCutsAsync(run, text, merged, answers, seen, ct);
    }

    private async Task<SpecRevisionVerdict> ApplyCutsAsync(
        SpecReviewRun run, SpecDraft text, SpecReviewMerged merged, IReadOnlyList<SpecAttackResult?> answers,
        SpecRoundEvidenceResult seen, CancellationToken ct)
    {
        var cut = await reviser.ReviseAsync(text, merged.Cuts, [], lastRound: false, run.Agent,
            PipelineCostTracker.GetOrCreate(run.Pipeline), ct);
        return cut.Refusal is { } refusal
            ? new(SpecRevisionOutcome.CutsNotApplied, text, merged, answers, seen, refusal)
            : new(SpecRevisionOutcome.Cleared, cut.Revised with { Repos = text.Repos }, merged, answers, seen);
    }
}
