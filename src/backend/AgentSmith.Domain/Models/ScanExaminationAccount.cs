namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-03e1: what this scan examined and what it cited for it, per entry group and
/// per station of a request.
/// <para>
/// It is a REPORTING surface, exactly like the entry map it stands on. It is rendered
/// beside the findings and raises findings of its own, and it never reaches the ledger the
/// delivery gate reads: a station the scan could not examine for want of an input it was
/// never given would be outstanding forever, and gating on it would fail every scan of
/// every repository that ships without a threat model.
/// </para>
/// <para>
/// <paramref name="CatalogueVersion"/> travels with it because the standard's current
/// release renumbered its predecessor: an id without the version that issued it cites
/// nothing. <paramref name="Attribution"/> is the licence line the ingested text carries.
/// </para>
/// </summary>
public sealed record ScanExaminationAccount(
    string CatalogueVersion,
    string Attribution,
    IReadOnlyList<EntryGroupExamination> Groups)
{
    public static ScanExaminationAccount Empty { get; } = new(string.Empty, string.Empty, []);

    public bool IsEmpty => Groups.Count == 0;

    /// <summary>Every station of every attempted group.</summary>
    public IReadOnlyList<StationExamination> Stations => [.. Groups.SelectMany(g => g.Stations)];

    /// <summary>The groups the run never reached — a budget fact, not a verdict.</summary>
    public IReadOnlyList<EntryGroupExamination> NotAttempted => [.. Groups.Where(g => !g.Attempted)];

    public int ExaminedCount => Stations.Count(station => station.Examined);

    /// <summary>Every finding whose citation resolved, carrying the group it belongs to.</summary>
    public IReadOnlyList<(string Group, CitedFindingRow Row)> Located =>
        [.. Groups.SelectMany(g => g.Located.Select(row => (g.Group, row)))];
}
