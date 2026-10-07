using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0399: a revision FULLY REPLACES the series — two cuts side by side are two truths.
/// 2026-10-06-03c7d: <c>specs/planned/</c> is shared by every series, so what a revision leaves
/// behind is selected by the series' base: every planned file belonging to a member of this
/// series that the current render did not write — a dropped spec, a relabelled one's old name and
/// an amendment's leftovers alike. A design mock of a spec still in the series is a companion the
/// renderer never writes, and stays.
/// </summary>
public sealed class SeriesStaleFiles
{
    /// <param name="listed">The entries of <c>specs/planned/</c>, in whatever form the lister used.</param>
    public IReadOnlyList<string> Select(
        IReadOnlyList<string> listed, SpecSet set, IReadOnlyList<SpecSetFile> rendered)
    {
        ArgumentNullException.ThrowIfNull(set);
        var seriesBase = set.Series ?? string.Empty;
        var written = rendered.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
        return [.. listed
            .Select(p => $"{SeriesPaths.Planned}/{SeriesPaths.FileName(p)}")
            .Distinct(StringComparer.Ordinal)
            .Where(path => IsStale(path, seriesBase, set, written))];
    }

    private static bool IsStale(string path, string seriesBase, SpecSet set, HashSet<string> written)
    {
        var name = SeriesPaths.FileName(path);
        if (seriesBase.Length == 0 || !SeriesPaths.BelongsToSeries(name, seriesBase)) return false;
        if (written.Contains(path)) return false;
        return !(SpecPhaseMocks.IsMock(name)
            && set.Phases.Any(p => SeriesPaths.BelongsTo(name, p.PhaseId)));
    }
}
