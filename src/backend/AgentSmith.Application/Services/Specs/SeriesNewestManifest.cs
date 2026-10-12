namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-12-f5f7b: picks the sub-series whose manifest speaks for the set — its
/// <c>series_repos</c> is the membership and its series-wide fields are the set's.
/// <para>
/// One found sub-series is the set, as before. Legacy manifests only (no <c>series_repos</c>) found
/// in several repositories read the pointer's repository, as the one-repository reader did. Otherwise
/// the newest is the largest PARSED current revision time — clocks of different machines, accepted;
/// a manifest without a parsed time never wins, and a tie between manifests naming different
/// members is refused rather than chosen between.
/// </para>
/// </summary>
public sealed class SeriesNewestManifest
{
    public (SubSeriesRead? Newest, string? Why) Pick(IReadOnlyList<SubSeriesRead> found, string carryingRepo)
    {
        ArgumentNullException.ThrowIfNull(found);
        if (found.Count == 1) return (found[0], null);
        if (found.All(f => f.IsLegacy)) return CarryingOf(found, carryingRepo);

        var stamped = found.Where(f => f.Stamp is not null).ToList();
        if (stamped.Count == 0)
            return (null, $"the sub-series in {Names(found)} carry no readable revision time to tell the newest by");
        var newest = stamped.Max(f => f.Stamp!.Value);
        var top = stamped.Where(f => f.Stamp == newest).ToList();
        if (top.Select(MembersKey).Distinct(StringComparer.Ordinal).Count() > 1)
            return (null, $"the sub-series in {Names(top)} share the newest revision time and name different members");
        return (top.FirstOrDefault(f => f.Repo == carryingRepo) ?? top[0], null);
    }

    private static (SubSeriesRead?, string?) CarryingOf(IReadOnlyList<SubSeriesRead> found, string carryingRepo) =>
        found.FirstOrDefault(f => string.Equals(f.Repo, carryingRepo, StringComparison.Ordinal)) is { } carried
            ? (carried, null)
            : (null, $"one-repository series of the ticket lie in {Names(found)} and none in the carrying repository '{carryingRepo}'");

    private static string MembersKey(SubSeriesRead read) =>
        string.Join(",", read.Members.Order(StringComparer.Ordinal));

    private static string Names(IEnumerable<SubSeriesRead> reads) =>
        string.Join(", ", reads.Select(r => $"'{r.Repo}'"));
}
