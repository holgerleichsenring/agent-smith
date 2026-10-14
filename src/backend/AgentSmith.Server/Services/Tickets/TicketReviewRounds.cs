using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-8762l: review rows as the ticket page shows them. A run's rows — one per reviewer — are grouped by
/// (scope, spec, round) in event order, and the round's verdict is <see cref="SpecReviewMerge"/>'s, so the page
/// and the run cannot disagree on it. A conversation round already holds its merged lists.
/// </summary>
public static class TicketReviewRounds
{
    public static IReadOnlyList<TicketReviewRoundView> OfRun(IReadOnlyList<RunSpecReviewRound> rows) =>
        [.. rows.GroupBy(r => (r.Scope, r.SpecId, r.Round)).Select(Round)];

    public static TicketReviewRoundView OfConversation(ProposalReviewRound round)
    {
        ArgumentNullException.ThrowIfNull(round);
        return new(TicketReviewRoundView.Conversation, round.SpecId, round.Round, round.Verdict,
            [.. round.ReviewerVerdicts.Select((verdict, i) => new TicketReviewerView(i, verdict, [], [], [], []))],
            [.. round.Cuts.Select(TicketReviewCutView.Of)], [.. round.Findings.Select(TicketReviewFindingView.Of)],
            round.Residues, null, null);
    }

    private static TicketReviewRoundView Round(IGrouping<(string Scope, string? SpecId, int Round), RunSpecReviewRound> group)
    {
        var reviewers = group.OrderBy(r => r.Reviewer).Select(Reviewer).ToList();
        var merged = SpecReviewMerge.Merge([.. group.Select(r => (SpecAttackResult?)new SpecAttackResult(r.Verdict, Cuts(r), Findings(r)))]);
        return new(group.Key.Scope, group.Key.SpecId, group.Key.Round, merged.Verdict ?? group.First().Verdict,
            reviewers, [], [], [], group.Select(r => r.RevisionNumber).FirstOrDefault(n => n is not null), group.Max(r => r.At));
    }

    private static TicketReviewerView Reviewer(RunSpecReviewRound row) => new(
        row.Reviewer, row.Verdict, [.. Cuts(row).Select(TicketReviewCutView.Of)], [.. Findings(row).Select(TicketReviewFindingView.Of)],
        RunStoryJson.TryDeserialize<List<SpecEvidenceProblem>>(row.EvidenceProblemsJson) ?? [],
        RunStoryJson.TryDeserialize<List<SpecReviewResidue>>(row.ResiduesJson) ?? []);

    private static List<SpecReviewCut> Cuts(RunSpecReviewRound row) =>
        RunStoryJson.TryDeserialize<List<SpecReviewCut>>(row.CutsJson) ?? [];

    private static List<SpecReviewFinding> Findings(RunSpecReviewRound row) =>
        RunStoryJson.TryDeserialize<List<SpecReviewFinding>>(row.FindingsJson) ?? [];
}
