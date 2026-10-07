using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-10-06-03c7c: the series a filing or an amendment stores — a base id and the approved
/// drafts re-id'd onto it, made before anything renders, so a ticket never shows an id a model
/// made up. A new filing mints the base; an amendment keeps the one its ticket already carries.
/// </summary>
public sealed class FiledSeriesFactory(SeriesIdFactory ids, SeriesDraftIds draftIds)
{
    /// <summary>A freshly minted series of <paramref name="ordered"/>.</summary>
    public FiledSeries Mint(IReadOnlyList<PhaseDraft> ordered) => Under(ids.Mint(), ordered);

    /// <summary>The drafts re-id'd under a known base.</summary>
    public FiledSeries Under(string series, IReadOnlyList<PhaseDraft> ordered) =>
        new(series, draftIds.Assign(series, ordered));

    /// <summary>The base the ticket's approval record carries, or a fresh one for a record made
    /// before series existed.</summary>
    public string KeptOrMinted(SpecApprovalRecord? record) =>
        record?.Set.Series is { Length: > 0 } kept ? kept : ids.Mint();
}
