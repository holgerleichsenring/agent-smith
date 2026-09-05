using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// Runs the review over every phase of a derived set that no run has started, and turns its
/// findings into a corrected set plus the list that belongs to the author.
/// <para>
/// Only the unexecuted tail is reviewed. A phase that already ran is a record of work that
/// sits in the branch history, and correcting the contract it was judged by afterwards would
/// rewrite that record.
/// </para>
/// </summary>
public sealed class SpecReviewPass(ISpecReviewer reviewer, ILogger<SpecReviewPass> logger)
{
    public async Task<SpecReviewOutcome> RunAsync(
        SpecSet set, AgentConfig agent, BranchSearch? search,
        PipelineCostTracker costTracker, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);
        var phases = set.Phases.ToList();
        var corrected = new List<CriterionReview>();
        var forTheAuthor = new List<CriterionReview>();
        var reviews = new List<SpecReview>();

        foreach (var phase in set.UnexecutedTail)
        {
            var review = await reviewer.ReviewAsync(phase, agent, search, costTracker, cancellationToken);
            reviews.Add(review);
            if (review.Problem is not null)
            {
                logger.LogInformation(
                    "Spec review skipped phase {Phase}: {Problem}", phase.PhaseId, review.Problem);
                continue;
            }
            if (review.IsQuiet) continue;
            Absorb(phases, phase, review, corrected, forTheAuthor);
        }

        return new SpecReviewOutcome(set with { Phases = phases }, corrected, forTheAuthor, reviews);
    }

    private static void Absorb(
        List<SpecPhase> phases, SpecPhase phase, SpecReview review,
        List<CriterionReview> corrected, List<CriterionReview> forTheAuthor)
    {
        var (rewritten, unapplied) = SpecReviewCorrection.Apply(phase, review.Correctable);
        var index = phases.FindIndex(p => string.Equals(p.PhaseId, phase.PhaseId, StringComparison.Ordinal));
        if (index >= 0) phases[index] = rewritten;
        corrected.AddRange(review.Correctable.Except(unapplied));
        // An unapplied correction is not a smaller correction: the text would not carry it
        // exactly, so the criterion still says what the review objected to.
        forTheAuthor.AddRange(review.ForTheAuthor);
        forTheAuthor.AddRange(unapplied);
    }
}
