namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>2026-11-24-9dc0a: what the server's check found open after a conflict pass — the paths and the conflict marker
/// lines left. A pass made progress when fewer paths are open, or as many with fewer marker lines; both only fall, so a
/// conversation continued on progress ends.</summary>
public sealed record BaseMergeOpen(IReadOnlyList<BaseMergeOpenPath> Paths, int MarkerLines)
{
    public bool IsWhole => Paths.Count == 0;

    public IReadOnlyList<string> PathNames => [.. Paths.Select(p => p.Path)];

    /// <summary>Everything conflicted is open: before any pass, or when the check itself failed.</summary>
    public static BaseMergeOpen All(IReadOnlyList<string> conflicted, string why) =>
        new([.. conflicted.Select(p => new BaseMergeOpenPath(p, why))], int.MaxValue);

    public bool IsProgressOver(BaseMergeOpen earlier)
    {
        ArgumentNullException.ThrowIfNull(earlier);
        return Paths.Count < earlier.Paths.Count || (Paths.Count == earlier.Paths.Count && MarkerLines < earlier.MarkerLines);
    }

    public string Say() => string.Join("; ", Paths);
}
