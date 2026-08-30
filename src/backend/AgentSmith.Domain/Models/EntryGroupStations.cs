namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-18e3: one entry group of the scanned system and where its six stations live.
/// <para>
/// The grouping is the master's own judgement. A configured list of groups would be a
/// second thing to maintain and would rot exactly like the hand-authored architecture
/// models whose neglect is why comparable tooling goes unrun — so the master states the
/// grouping it chose and the run holds it to every station of each one.
/// </para>
/// </summary>
public sealed record EntryGroupStations(string Group, IReadOnlyList<StationLocation> Stations)
{
    /// <summary>The stations of this group nothing located.</summary>
    public IReadOnlyList<StationLocation> Unlocated => [.. Stations.Where(s => !s.Located)];
}
