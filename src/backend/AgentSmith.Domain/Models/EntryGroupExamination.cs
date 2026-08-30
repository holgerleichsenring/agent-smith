namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-03e1: one entry group of the scanned system, its six stations, and what each
/// of them examined. A group beyond the run's declared cap carries no stations at all and
/// says so through <paramref name="Attempted"/> — a budget fact, never a verdict.
/// </summary>
public sealed record EntryGroupExamination(
    string Group, bool Attempted, IReadOnlyList<StationExamination> Stations)
{
    /// <summary>The stations this group examined, by the rule the run can check.</summary>
    public IReadOnlyList<StationExamination> Examined => [.. Stations.Where(s => s.Examined)];

    /// <summary>Every finding of this group whose citation resolved.</summary>
    public IReadOnlyList<CitedFindingRow> Located => [.. Stations.SelectMany(s => s.Located)];
}
