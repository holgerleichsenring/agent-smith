using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762k: the run's ONE set-scope round, after 8762g's per-spec loop, over the cleared prefix
/// (<see cref="SeriesRoundScope"/>). One bounded call that may exceed the review share — never the run cap. Its
/// landing cuts are applied per member through 8762f's reviser, published as ONE revision, and only then is event
/// 87 published with Scope series, the residues and that revision number. A standing REFUSE is returned for the
/// step's single <see cref="ReviewExhaustion"/> decision, keyed by the prefix's first member. A round skipped or
/// not taken publishes nothing and says why on the step's line.
/// </summary>
public sealed class SeriesContractRound(
    ISeriesContractReviewer reviewer, DerivationLookFactory looks, ISpecCutReviser reviser, SpecCutResidues residues,
    SpecReviewRevision revision, SpecReviewRoundPublisher rounds, SpecReviewLoop loop)
{
    /// <summary>The run look: cleared members plus 12 looks, never below 8762e's 24.</summary>
    public static DerivationLookTerms Terms(int cleared) =>
        DerivationLookTerms.AttackReview with { Allowance = Math.Max(DerivationLookTerms.AttackReview.Allowance, cleared + 12) };

    /// <param name="reviewed">The specs 8762g's loop reviewed in this attempt.</param>
    /// <param name="contractClean">2026-11-10-8e59: a review ended clean on exactly this contract.</param>
    public async Task<SeriesContractRoundResult> RunAsync(
        SpecReviewRun run, IReadOnlyCollection<string> reviewed, bool contractClean, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(reviewed);
        var set = run.Pipeline.Get<SpecSet>(ContextKeys.SpecSet);
        var prefix = SeriesRoundScope.Prefix(set, run.Pipeline);
        if (SeriesRoundScope.Skip(set, prefix, reviewed, contractClean) is { } skip) return new($"series contract round skipped: {skip}");
        var tracker = PipelineCostTracker.GetOrCreate(run.Pipeline);
        if (tracker.IsBudgetExhausted) return NotTaken(prefix, CostCapStop.Describe(tracker), contractClean);
        var input = new SeriesContractInput(set.Contract!, SeriesRoundScope.Members(set, prefix), InRun: true, run.QuestionPin)
        {
            RepositoryNames = SpecReviewRepositories.Names(run.Pipeline),
        };
        SeriesContractReview review;
        await using (var look = looks.OnTerms(run.Pipeline, Terms(prefix.Count)))
            review = await reviewer.ReviewAsync(input, look, run.Agent, tracker, ct);
        if (review.Result is not { } result) return NotTaken(prefix, review.NotTaken ?? "no answer", contractClean);
        var split = SeriesRoundScope.Split(result, set, prefix, reviewed);
        return await LandAsync(run, prefix, split, [.. review.Dropped, .. split.Dropped], ct);
    }

    // 2026-11-10-8e59: a contract no review ended clean on is uncleared, like a spec: the attempt stops before the
    // prefix, so nothing it governs is built on it. A contract already reviewed clean only loses this round's member check.
    private static SeriesContractRoundResult NotTaken(IReadOnlyList<DerivedSpec> prefix, string why, bool contractClean) =>
        new($"series contract round not taken: {why}")
        {
            Uncleared = contractClean ? null : new SpecSequenceStop(prefix[0].SpecId, $"the series contract was not reviewed: {why}", Park: false),
        };

    private async Task<SeriesContractRoundResult> LandAsync(
        SpecReviewRun run, IReadOnlyList<DerivedSpec> prefix, SeriesRoundSplit split,
        IReadOnlyList<SpecEvidenceProblem> problems, CancellationToken ct)
    {
        var landing = split.Landing;
        var changed = new List<SpecDraft>();
        var standing = new List<SpecReviewResidue>();
        foreach (var member in prefix)
        {
            var cuts = landing.Cuts.Where(cut => cut.SpecId == member.SpecId).ToList();
            if (cuts.Count == 0) continue;
            var revised = await reviser.ReviseAsync(member.Draft, cuts, [], lastRound: true, run.Agent,
                PipelineCostTracker.GetOrCreate(run.Pipeline), ct);
            standing.AddRange(residues.Of(revised.Revised, cuts, []));
            if (!string.Equals(revised.Revised.Yaml, member.Draft.Yaml, StringComparison.Ordinal)) changed.Add(revised.Revised);
        }
        int? number = null;
        if (changed.Count > 0)
        {
            var published = await revision.PublishAsync(run.Pipeline, changed, SpecRevisionCause.SeriesContractReview, ct);
            if (!published.IsSuccess) return new("series contract round failed", Failure: published.Message);
            number = run.Pipeline.Get<SpecSet>(ContextKeys.SpecSet).Current.Number;
        }
        await rounds.PublishAsync(new SpecReviewRound(null, SpecReviewScopes.Series, 1, problems, standing, number), [landing], ct);
        // 2026-11-10-8e59: the same bar as a spec — a refuse, a cut that could not land, or a cut wording still standing
        // leaves the contract not reviewed clean, so the author is asked.
        var objection = landing.Verdict == SpecReviewVerdicts.Refuse || split.Unapplied > 0 || standing.Count > 0
            ? new StandingObjection(null, [.. landing.Findings,
                .. problems.Select(p => new SpecReviewFinding($"{p.Claim}: {p.Problem}", "series round")),
                .. standing.Select(r => new SpecReviewFinding($"cut wording still stands in {r.Register}: \"{r.Quote}\"", "series round"))],
                prefix[0].SpecId)
            {
                TextHash = Contracts.Specs.ReviewedText.Hash(run.Pipeline.Get<SpecSet>(ContextKeys.SpecSet).Contract!),
            }
            : null;
        // 2026-11-10-8e59: a member this round's cuts changed is no longer cleared; its new text is attacked now, in
        // rounds of its own the budget may still stop, and what that leaves uncleared is handled like any spec.
        var reattacked = new List<SpecReviewOutcome>();
        foreach (var draft in changed)
            reattacked.Add(await loop.RunAsync(run, run.Pipeline.Get<SpecSet>(ContextKeys.SpecSet).Specs.First(s => s.SpecId == draft.SpecId),
                first: false, ct));
        return new($"series contract round over {prefix.Count} cleared member(s): {landing.Verdict}"
            + (changed.Count > 0 ? $", revised {string.Join(", ", changed.Select(d => d.SpecId))}" : string.Empty)
            + (standing.Count > 0 ? $", {standing.Count} residue(s) standing" : string.Empty), objection)
        {
            Reattacked = reattacked,
        };
    }
}
