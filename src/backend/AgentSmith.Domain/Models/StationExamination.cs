namespace AgentSmith.Domain.Models;

/// <summary>
/// 2026-08-30-03e1: one station of one entry group and what the scan can show for it.
/// <para>
/// EXAMINED HAS A MECHANICAL REFERENT and is never a fresh assertion by the model. It
/// holds when the station's own located citation stands — a file the scan read, a line
/// inside it — AND the read set holds other files beneath that file. A scan that opened
/// exactly one class and nothing around it looked at a station; it did not examine one,
/// and <paramref name="Note"/> says which of the two happened.
/// </para>
/// </summary>
public sealed record StationExamination(
    VerificationStation Station,
    bool Examined,
    string Note,
    IReadOnlyList<CitedFindingRow> Cited)
{
    /// <summary>The findings whose citation resolved: what this station yielded.</summary>
    public IReadOnlyList<CitedFindingRow> Located => [.. Cited.Where(row => row.Located)];

    /// <summary>The findings whose citation resolved against nothing — reported, never
    /// delivered, because a claim resting on an unread file is silence.</summary>
    public IReadOnlyList<CitedFindingRow> Unlocated => [.. Cited.Where(row => !row.Located)];
}
