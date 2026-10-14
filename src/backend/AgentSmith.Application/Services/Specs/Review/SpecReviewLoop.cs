using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762g: one spec's rounds before it is built. A round runs the panel on the current text, merges
/// it, applies the cuts through the reviser (the last round may move a broken fact), names the residues, and
/// when the text changed publishes it as one revision and checks its evidence once more — that result is both
/// the stop input and the next round's evidence. A REFUSE without cuts is rewritten once from the code and a
/// fresh round follows; a second one stands. Each reviewer's answer is stored after the round's cuts. The
/// share is not checked for the first reviewed spec; the run cap and the wall-time ceiling always are.
/// </summary>
public sealed class SpecReviewLoop(
    SpecReviewPanel panel, ISpecRoundEvidence evidence, ISpecCutReviser reviser, SpecCutResidues residues,
    ISpecRederiver rederiver, SpecReviewRoundPublisher rounds, SpecReviewRevision revision)
{
    public async Task<SpecReviewOutcome> RunAsync(SpecReviewRun run, DerivedSpec spec, bool first, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(spec);
        var state = new SpecReviewState(spec.Draft, await CheckAsync(run, spec.Draft, ct));
        while (state.Round < run.Settings.Review.Rounds)
        {
            if (BudgetStop(run, first) is { } stop) return state.Standing(budgetStop: stop);
            state.Round++;
            var answers = await panel.ReviewAsync(run.Pipeline, Input(run, state), run.Settings.Review, run.Agent, ct);
            state.Merged = SpecReviewMerge.Merge(answers);
            if (!state.Merged.IsTaken) return state.Standing();
            var ended = state.Merged is { Verdict: SpecReviewVerdicts.Refuse, Cuts.Count: 0 }
                ? await RewriteAsync(run, state, answers, ct)
                : await ApplyAsync(run, state, answers, ct);
            if (ended is not null) return ended;
        }
        return state.Standing();
    }

    private static string? BudgetStop(SpecReviewRun run, bool first) =>
        (first ? null : SpecReviewBudget.Spent(run.Pipeline, run.Settings))
        ?? SpecReviewBudget.Exhausted(run.Pipeline, run.Settings);

    private async Task<SpecReviewOutcome?> ApplyAsync(
        SpecReviewRun run, SpecReviewState state, IReadOnlyList<SpecAttackResult?> answers, CancellationToken ct)
    {
        var merged = state.Merged!;
        var seen = state.Evidence;
        var revised = await reviser.ReviseAsync(state.Draft, merged.Cuts, seen.Problems,
            state.Round == run.Settings.Review.Rounds, run.Agent, PipelineCostTracker.GetOrCreate(run.Pipeline), ct);
        state.Residues = residues.Of(revised.Revised, merged.Cuts, seen.Problems);
        var changed = !string.Equals(revised.Revised.Yaml, state.Draft.Yaml, StringComparison.Ordinal);
        var number = changed ? await ReviseAsync(run, state, revised.Revised, merged.Verdict!, ct) : null;
        if (changed && number is null) return state.Standing(failure: state.Failure);
        await PublishRoundAsync(state, seen, answers, number, ct);
        if (SpecReviewStopRule.IsDone(merged, state.Residues, state.Evidence)) return state.Settled();
        // A round whose cuts changed nothing ends the loop: another reviewer over the same text is shopping.
        return changed ? null : state.Standing();
    }

    private async Task<SpecReviewOutcome?> RewriteAsync(
        SpecReviewRun run, SpecReviewState state, IReadOnlyList<SpecAttackResult?> answers, CancellationToken ct)
    {
        var seen = state.Evidence;
        var rewrite = state.Rederived ? null
            : await rederiver.RederiveAsync(run.Pipeline, state.Draft, state.Merged!.Findings, run.WiderGoal, run.Agent, ct);
        state.Rederived = true;
        state.Residues = [];
        var number = rewrite is null ? null : await ReviseAsync(run, state, rewrite, "refuse, rewritten from the code", ct);
        if (rewrite is not null && number is null) return state.Standing(failure: state.Failure);
        await PublishRoundAsync(state, seen, answers, number, ct);
        return rewrite is null ? state.Standing() : null;
    }

    /// <summary>Publishes the changed text as one revision and re-checks its evidence; null when the branch did
    /// not take it, the failure then on the state.</summary>
    private async Task<int?> ReviseAsync(
        SpecReviewRun run, SpecReviewState state, Contracts.Models.SpecDraft text, string verdict, CancellationToken ct)
    {
        var cause = $"spec review round {state.Round} of {state.Draft.SpecId}: {verdict}";
        var published = await revision.PublishAsync(run.Pipeline, [text], cause, ct);
        if (!published.IsSuccess) return Fail(state, published.Message);
        state.Draft = text with { Repos = state.Draft.Repos };
        state.Evidence = await CheckAsync(run, state.Draft, ct);
        return run.Pipeline.Get<SpecSet>(ContextKeys.SpecSet).Current.Number;
    }

    private static int? Fail(SpecReviewState state, string failure)
    {
        state.Failure = failure;
        return null;
    }

    private Task PublishRoundAsync(
        SpecReviewState state, SpecRoundEvidenceResult seen, IReadOnlyList<SpecAttackResult?> answers, int? number,
        CancellationToken ct) =>
        rounds.PublishAsync(new SpecReviewRound(state.Draft.SpecId, SpecReviewScopes.Spec, state.Round, seen.Problems,
            state.Residues, number), answers, ct);

    private Task<SpecRoundEvidenceResult> CheckAsync(SpecReviewRun run, Contracts.Models.SpecDraft draft, CancellationToken ct) =>
        evidence.CheckAsync(run.Pipeline, draft, run.Pipeline.Get<SpecSet>(ContextKeys.SpecSet), ct);

    private static SpecAttackInput Input(SpecReviewRun run, SpecReviewState state) =>
        new(state.Draft, SpecReviewScopes.Spec, state.Round, 0, run.WiderGoal,
            SpecToday.Describes(state.Draft, run.Pipeline.Get<SpecSet>(ContextKeys.SpecSet)),
            state.Evidence.Problems, state.Evidence.Unchecked, state.Evidence.Total, state.Residues)
        {
            RepositoryNames = SpecReviewRepositories.Names(run.Pipeline),
        };
}
