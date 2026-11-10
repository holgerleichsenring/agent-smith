using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-06-03c7c: the series a filing or an amendment stores — a base id and the approved
/// drafts re-id'd onto it, made before anything renders, so a ticket never shows an id a model
/// made up. A new filing mints the base; an amendment keeps the one its ticket already carries.
/// <para>2026-10-13-8762j: an epic source's series contract is re-keyed with the same map as the drafts and
/// narrowed to the filed members — a follow-up that left executed children out files a contract without them.
/// 2026-10-13-8762l: so are the source's review rounds (<see cref="ApprovedReviewRounds.Rekey"/>).</para>
/// </summary>
public sealed class FiledSeriesFactory(SeriesIdFactory ids, SeriesDraftIds draftIds, ILogger<FiledSeriesFactory> logger)
{
    /// <summary>A freshly minted series of <paramref name="ordered"/>.</summary>
    /// <param name="source">The approved proposal the drafts come from; its contract, if an epic carries one, is filed.</param>
    public FiledSeries Mint(IReadOnlyList<SpecDraft> ordered, OutcomeProposal? source) => Under(ids.Mint(), ordered, source);

    /// <summary>The drafts re-id'd under a known base.</summary>
    public FiledSeries Under(string series, IReadOnlyList<SpecDraft> ordered, OutcomeProposal? source)
    {
        var map = SeriesDraftIds.Map(series, ordered);
        var drafts = draftIds.Assign(series, ordered);
        var filed = new FiledSeries(series, drafts)
        {
            ReviewRounds = ApprovedReviewRounds.Rekey(source?.ReviewRounds ?? [], map, logger, series),
            ReviewedClean = CleanFiled(ordered, drafts, map, source),
            Sources = map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal),
        };
        if ((source as EpicOutcome)?.Contract is not { } contract) return filed;
        var within = contract.Rekeyed(map).Within(filed.Drafts.Select(d => d.SpecId));
        if (within.Removed.Count > 0)
            logger.LogInformation("Series {Series}: contract entries naming no filed spec removed: {Removed}",
                series, string.Join("; ", within.Removed));
        // 2026-11-10-8e59: the contract's clean review follows our rename, and only when no entry was narrowed away.
        var cleanContract = within.Removed.Count == 0 && within.Contract is { } kept && ReviewedText.Covers(source!.ReviewedClean, contract)
            ? [ReviewedText.Of(kept)] : Array.Empty<ReviewedText>();
        return filed with { Contract = within.Contract, ReviewedClean = [.. filed.ReviewedClean, .. cleanContract] };
    }

    // 2026-11-10-8e59: filing re-ids a draft, which is our rename and no change of content, so a clean review moves
    // to the filed id and yaml — only when the source draft is exactly the text that review ended clean on.
    private static List<ReviewedText> CleanFiled(IReadOnlyList<SpecDraft> ordered, IReadOnlyList<SpecDraft> drafts,
        IReadOnlyDictionary<string, string> map, OutcomeProposal? source) =>
        [.. ordered.Where(d => ReviewedText.Covers(source?.ReviewedClean, d.SpecId, d.Yaml) && map.ContainsKey(d.SpecId))
            .Select(d => drafts.FirstOrDefault(f => f.SpecId == map[d.SpecId]))
            .OfType<SpecDraft>()
            .Select(f => ReviewedText.Of(f.SpecId, f.Yaml))];

    /// <summary>The base the ticket's approval record carries, or a fresh one for a record made
    /// before series existed.</summary>
    public string KeptOrMinted(SpecApprovalRecord? record) =>
        record?.Set.Series is { Length: > 0 } kept ? kept : ids.Mint();
}
