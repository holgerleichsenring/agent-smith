using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-06-03c7c: which series a run's set belongs to. The base is read, never re-derived:
/// from the set the run works from (the branch's, or the one an approval hands over), then from
/// the approval record, then from the pointer row that caches it — and only a ticket none of
/// them knows gets a freshly minted one. The branch comes first, so a run without a database
/// finds the same base on its next run.
/// </summary>
public sealed class SeriesResolver(SeriesIdFactory ids)
{
    public string Resolve(SpecSet? working, SpecApprovalRecord? approval, SpecSetPointer? pointer) =>
        Known(working?.Series) ?? Known(approval?.Set.Series) ?? Known(pointer?.SeriesId) ?? ids.Mint();

    private static string? Known(string? series) => string.IsNullOrWhiteSpace(series) ? null : series;
}
