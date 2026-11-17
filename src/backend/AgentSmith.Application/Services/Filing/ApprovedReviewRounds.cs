using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-10-13-8762l: the design conversation's review rounds as the approval record keeps them. At filing
/// they are re-keyed with the map the drafts' ids took — a round of a filed draft under its filed id, the
/// series round under <see cref="ProposalReviewTokens.Series"/> with its cuts' members re-keyed, every other
/// round (the epic parent's own, a draft the filing left out) dropped and logged. On the record the new
/// rounds of a spec replace its old ones, and an old round is kept only for an id the continuation marks
/// kept, so a dropped or reused id never inherits rounds of text it does not carry.
/// </summary>
public static class ApprovedReviewRounds
{
    /// <param name="dropped">Each round no filed spec carries, as "id round n", for the caller to log.</param>
    public static IReadOnlyList<ProposalReviewRound> Rekey(
        IReadOnlyList<ProposalReviewRound> rounds, IReadOnlyDictionary<string, string> map, out IReadOnlyList<string> dropped)
    {
        ArgumentNullException.ThrowIfNull(rounds);
        ArgumentNullException.ThrowIfNull(map);
        var kept = new List<ProposalReviewRound>();
        var left = new List<string>();
        foreach (var round in rounds)
        {
            if (round.SpecId == ProposalReviewTokens.Series)
                kept.Add(round with { Cuts = [.. round.Cuts.Select(c => c.SpecId is { } id && map.TryGetValue(id, out var to) ? c with { SpecId = to } : c)] });
            else if (map.TryGetValue(round.SpecId, out var filed))
                kept.Add(round with { SpecId = filed });
            else
                left.Add($"{round.SpecId} round {round.Round}");
        }
        dropped = left;
        return kept;
    }

    /// <summary>The rounds the new record stores; null when there are none.</summary>
    public static IReadOnlyList<ProposalReviewRound>? Merge(
        SpecApprovalRecord? previous, FiledSeries series, SeriesContinuation? continuation)
    {
        ArgumentNullException.ThrowIfNull(series);
        var renewed = series.ReviewRounds.Select(r => r.SpecId).ToHashSet(StringComparer.Ordinal);
        var kept = (continuation?.Dispositions ?? []).Where(d => d.Kind == SpecDispositionKinds.Kept)
            .Select(d => d.SpecId).ToHashSet(StringComparer.Ordinal);
        List<ProposalReviewRound> all =
        [
            .. (previous?.ReviewRounds ?? []).Where(r => kept.Contains(r.SpecId) && !renewed.Contains(r.SpecId)),
            .. series.ReviewRounds,
        ];
        return all.Count > 0 ? all : null;
    }
}
