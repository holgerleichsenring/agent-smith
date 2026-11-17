using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Filing;

/// <summary>
/// 2026-10-06-03c7c: the series a filing or an amendment stores — a base id and the approved
/// drafts re-id'd onto it, made before anything renders, so a ticket never shows an id a model
/// made up. A new filing mints the base; an amendment keeps the one its ticket already carries.
/// <para>2026-10-13-8762j: an epic source's series contract is re-keyed with the same map as the drafts and
/// narrowed to the filed members — a follow-up that left executed children out files a contract without them.
/// 2026-10-13-8762l: so are the source's review rounds (<see cref="ApprovedReviewRounds.Rekey"/>).</para>
/// </summary>
public sealed class FiledSeriesFactory(
    SeriesIdFactory ids, SeriesDraftIds draftIds, ISeriesSettingsReader settings, ILogger<FiledSeriesFactory> logger)
{
    /// <summary>A freshly minted series of <paramref name="ordered"/>.</summary>
    /// <param name="source">The approved proposal the drafts come from; its contract, if an epic carries one, is filed.</param>
    public FiledSeries Mint(IReadOnlyList<SpecDraft> ordered, OutcomeProposal? source) => Under(ids.Mint(), ordered, source);

    /// <summary>
    /// 2026-11-12-cd4cc: <paramref name="ordered"/> as one series, or as several when it holds more than one series
    /// may (series.max_specs) — each a fresh series of the next drafts in order, whose requires on an earlier part
    /// name that part's filed ids.
    /// </summary>
    public IReadOnlyList<FiledSeries> MintParts(IReadOnlyList<SpecDraft> ordered, OutcomeProposal? source)
    {
        ArgumentNullException.ThrowIfNull(ordered);
        var size = Math.Clamp(settings.Current().Series.MaxSpecs, 1, SeriesConfig.MemberLetters);
        var parts = new List<FiledSeries>();
        var earlier = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var chunk in ordered.Chunk(size))
        {
            var part = Mint(draftIds.Rewire(chunk, earlier), source);
            foreach (var (filedId, placeholder) in part.Sources) earlier[placeholder] = filedId;
            parts.Add(part);
        }
        return parts;
    }

    /// <summary>The drafts re-id'd under a known base.</summary>
    public FiledSeries Under(string series, IReadOnlyList<SpecDraft> ordered, OutcomeProposal? source)
    {
        var map = SeriesDraftIds.Map(series, ordered);
        var drafts = draftIds.Assign(series, ordered);
        var rounds = ApprovedReviewRounds.Rekey(source?.ReviewRounds ?? [], map, out var dropped);
        if (dropped.Count > 0)
            logger.LogInformation("Series {Series}: review rounds of no filed spec dropped: {Dropped}", series, string.Join(", ", dropped));
        var filed = new FiledSeries(series, drafts)
        {
            ReviewRounds = rounds,
            ReviewedClean = CleanFiled(ordered, drafts, map, rounds, source),
            Sources = map.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal),
        };
        if ((source as EpicOutcome)?.Contract is not { } contract) return filed;
        var within = contract.Rekeyed(map).Within(filed.Drafts.Select(d => d.SpecId));
        if (within.Removed.Count > 0)
            logger.LogInformation("Series {Series}: contract entries naming no filed spec removed: {Removed}",
                series, string.Join("; ", within.Removed));
        // 2026-11-10-8e59: the contract's clean review follows our rename, and only when no entry was narrowed away.
        // 2026-11-17-5701e: so does a pass of the turn's last series round.
        var cleanContract = within.Removed.Count == 0 && within.Contract is { } kept
            && (ReviewedText.Covers(source!.ReviewedClean, contract) || PassedInTurn(ProposalReviewTokens.Series, ProposalReviewTokens.Series, rounds, source))
            ? [ReviewedText.Of(kept)] : Array.Empty<ReviewedText>();
        return filed with { Contract = within.Contract, ReviewedClean = [.. filed.ReviewedClean, .. cleanContract] };
    }

    // 2026-11-10-8e59: filing re-ids a draft, which is our rename and no change of content, so a clean review moves
    // to the filed id and yaml — when the source draft is exactly the text that review ended clean on.
    // 2026-11-17-5701e: or when the approving turn's last round of the draft passed it (build, build with cuts, or a
    // refusal waiting only on an owner decision), whatever applying its cuts made of the text.
    private static List<ReviewedText> CleanFiled(IReadOnlyList<SpecDraft> ordered, IReadOnlyList<SpecDraft> drafts,
        IReadOnlyDictionary<string, string> map, IReadOnlyList<ProposalReviewRound> rounds, OutcomeProposal? source) =>
        [.. ordered.Where(d => map.ContainsKey(d.SpecId)
                && (ReviewedText.Covers(source?.ReviewedClean, d.SpecId, d.Yaml) || PassedInTurn(d.SpecId, map[d.SpecId], rounds, source)))
            .Select(d => drafts.FirstOrDefault(f => f.SpecId == map[d.SpecId]))
            .OfType<SpecDraft>()
            .Select(f => ReviewedText.Of(f.SpecId, f.Yaml))];

    // The source's own id is what NotReviewed names; the re-keyed rounds sit under the filed id.
    private static bool PassedInTurn(string sourceId, string filedId, IReadOnlyList<ProposalReviewRound> rounds, OutcomeProposal? source) =>
        source is not null && source.NotReviewed?.Contains(sourceId) != true && ProposalReviewText.LastRoundPassed(rounds, filedId);

    /// <summary>The base the ticket's approval record carries, or a fresh one for a record made
    /// before series existed.</summary>
    public string KeptOrMinted(SpecApprovalRecord? record) =>
        record?.Set.Series is { Length: > 0 } kept ? kept : ids.Mint();
}
