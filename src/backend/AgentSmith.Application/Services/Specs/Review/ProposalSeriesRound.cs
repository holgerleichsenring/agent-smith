using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762k: a design turn's series round — FINDINGS ONLY. After the per-spec rounds of an epic with a
/// contract, inside the turn's review share, one fresh reviewer over the parent's attack look (the parent
/// aggregates the set); nothing is re-prompted. Each admitted cut becomes a <see cref="Problem"/> finding on its
/// member, the verdict and every set-wide finding one under the parent, and ONE round is recorded under
/// <see cref="ProposalReviewTokens.Series"/> — never the parent's id, which keys the parent's own rounds. A round
/// not taken leaves a finding under the parent and the series token in NotReviewed, so it cannot be approved.
/// </summary>
public sealed class ProposalSeriesRound(
    ISeriesContractReviewer reviewer, DerivationLookFactory looks, ILogger<ProposalSeriesRound> logger)
{
    public const string Problem = "series-contract";

    internal async Task<ProposalReviewOutcome> AddAsync(
        ProposalReviewSession session, ProposalReviewOutcome outcome, CancellationToken ct)
    {
        if (outcome.Proposal is not EpicOutcome { Contract: { IsEmpty: false } contract } epic) return outcome;
        var turn = session.Turn;
        var spent = SpecReviewBudget.Exhausted(turn.Pipeline, session.Settings) ?? SpecReviewBudget.Spent(turn.Pipeline, session.Settings);
        var review = spent is not null ? SeriesContractReview.Failed(spent) : await ReviewAsync(turn, epic, contract, ct);
        var parent = epic.Parent.SpecId;
        if (review.Result is not { } result)
        {
            logger.LogInformation("The design turn's series contract round was not taken: {Why}", review.NotTaken);
            return outcome with
            {
                Standing = [.. outcome.Standing, new ProposalFinding(parent, Problem,
                    $"the series contract was not reviewed yet ({review.NotTaken}); it cannot be approved until a later turn reviews it")],
                NotReviewed = [.. outcome.NotReviewed ?? [], ProposalReviewTokens.Series],
            };
        }
        var round = new ProposalReviewRound(ProposalReviewTokens.Series, 1, result.Verdict, [result.Verdict],
            result.Cuts, result.Findings, []);
        return outcome with { Rounds = [.. outcome.Rounds, round], Standing = [.. outcome.Standing, .. Findings(result, parent)] };
    }

    private async Task<SeriesContractReview> ReviewAsync(
        ProposalTurn turn, EpicOutcome epic, Contracts.Specs.SeriesContract contract, CancellationToken ct)
    {
        var input = new SeriesContractInput(contract, [.. epic.Children.Select(child => new SeriesContractMember(child))], InRun: false)
        {
            RepositoryNames = SpecReviewRepositories.Names(turn.Pipeline),
        };
        await using var look = looks.ForProposalAttack(turn.Pipeline, epic.Parent);
        return await reviewer.ReviewAsync(input, look, turn.Agent, turn.CostTracker, ct);
    }

    private static IEnumerable<ProposalFinding> Findings(SpecAttackResult result, string parent)
    {
        foreach (var cut in result.Cuts)
            yield return new ProposalFinding(cut.SpecId!, Problem,
                $"{cut.Register}: replace \"{cut.Quote}\" {(cut.Replacement is null ? "by nothing" : $"with \"{cut.Replacement}\"")}",
                cut.Quote, cut.Evidence);
        if (result.Verdict != SpecReviewVerdicts.Build)
            yield return new ProposalFinding(parent, Problem,
                $"the series contract review answered {result.Verdict}: {result.Cuts.Count} cut(s), {result.Findings.Count} finding(s)");
        foreach (var finding in result.Findings)
            yield return new ProposalFinding(parent, Problem, finding.Text, null, finding.Evidence);
    }
}
