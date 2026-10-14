using AgentSmith.Application.Services.Specs;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-8762m: the build note an executed spec's record carries — the four lists, or
/// <see cref="Missing"/> when its master left none.
/// </summary>
public sealed record TicketSeriesBuildNoteView(
    bool Missing,
    IReadOnlyList<string> Renamed,
    IReadOnlyList<string> Moved,
    IReadOnlyList<string> Persisted,
    IReadOnlyList<string> Deviations)
{
    /// <summary>The view of a record's note; null when the record carries no hand_forward key.</summary>
    public static TicketSeriesBuildNoteView? Of(SpecBuildNoteRead read) => read switch
    {
        { Note: { } note } => new(false, note.Renamed, note.Moved, note.Persisted, note.Deviations),
        { Missing: true } => new(true, [], [], [], []),
        _ => null,
    };
}
