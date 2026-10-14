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
    SpecReviewRevision revision, SpecReviewRoundPublisher rounds)
{
    /// <summary>The run look: cleared members plus 12 looks, never below 8762e's 24.</summary>
    public static DerivationLookTerms Terms(int cleared) =>
        DerivationLookTerms.AttackReview with { Allowance = Math.Max(DerivationLookTerms.AttackReview.Allowance, cleared + 12) };

    /// <param name="reviewed">The specs 8762g's loop reviewed in this attempt.</param>
    public async Task<SeriesContractRoundResult> RunAsync(
        SpecReviewRun run, IReadOnlyCollection<string> reviewed, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(reviewed);
        var set = run.Pipeline.Get<SpecSet>(ContextKeys.SpecSet);
        var prefix = SeriesRoundScope.Prefix(set, run.Pipeline);
        if (SeriesRoundScope.Skip(set, prefix, reviewed) is { } skip) return new($"series contract round skipped: {skip}");
        var tracker = PipelineCostTracker.GetOrCreate(run.Pipeline);
        if (tracker.IsBudgetExhausted) return new($"series contract round not taken: {CostCapStop.Describe(tracker)}");
        var input = new SeriesContractInput(set.Contract!, SeriesRoundScope.Members(set, prefix), InRun: true, run.QuestionPin)
        {
            RepositoryNames = SpecReviewRepositories.Names(run.Pipeline),
        };
        SeriesContractReview review;
        await using (var look = looks.OnTerms(run.Pipeline, Terms(prefix.Count)))
            review = await reviewer.ReviewAsync(input, look, run.Agent, tracker, ct);
        if (review.Result is not { } result) return new($"series contract round not taken: {review.NotTaken}");
        var split = SeriesRoundScope.Split(result, set, prefix, reviewed);
        return await LandAsync(run, prefix, split, [.. review.Dropped, .. split.Dropped], ct);
    }

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
        var objection = landing.Verdict == SpecReviewVerdicts.Refuse
            ? new StandingObjection(null, landing.Findings, prefix[0].SpecId)
            : null;
        return new($"series contract round over {prefix.Count} cleared member(s): {landing.Verdict}"
            + (changed.Count > 0 ? $", revised {string.Join(", ", changed.Select(d => d.SpecId))}" : string.Empty)
            + (standing.Count > 0 ? $", {standing.Count} residue(s) standing" : string.Empty), objection);
    }
}
