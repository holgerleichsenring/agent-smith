namespace AgentSmith.Contracts.Dialogue;

/// <summary>A ticket's series as its next attempt will read it, or why it could not be read.</summary>
/// <param name="Failure">Why nothing was read — the tracker did not answer; null on a read.</param>
/// <param name="SeriesId">The series' base id, or null when nothing named one.</param>
/// <param name="Specs">Every spec in series order.</param>
/// <param name="ShaByRepo">The ticket branch's revision per repository, from the latest attempt's pointer; empty
/// when no attempt published one.</param>
public sealed record SeriesReading(
    string? Failure, string? SeriesId, IReadOnlyList<SeriesEntry> Specs, IReadOnlyDictionary<string, string> ShaByRepo)
{
    public static SeriesReading Failed(string why) => new(why, null, [], new Dictionary<string, string>());
}
