using System.Text;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-13-8762m: renders the build notes of the specs that already ran, for the next spec's master
/// and its premise check. Bounded: the oldest notes are dropped first until the section fits, and one
/// line names each dropped spec and its record, which stays one read away.
/// </summary>
public static class SpecBuildNoteSection
{
    public const int MaxChars = 8000;
    public const string Heading = "## Build notes of the specs that already ran";
    public const string NoNote = "no note";

    public static string Render(IReadOnlyList<PrecedingNote> notes, int maxChars = MaxChars)
    {
        ArgumentNullException.ThrowIfNull(notes);
        if (notes.Count == 0) return string.Empty;
        var blocks = notes.Select(Block).ToList();
        var dropped = 0;
        string text;
        do text = Compose(notes, blocks, dropped);
        while (text.Length > maxChars && ++dropped <= notes.Count);
        return text;
    }

    private static string Compose(IReadOnlyList<PrecedingNote> notes, IReadOnlyList<string> blocks, int dropped)
    {
        var sb = new StringBuilder("\n").Append(Heading).Append('\n');
        if (dropped > 0)
            sb.Append("Dropped to keep this section short, oldest first — read their record: ")
                .Append(string.Join(", ", notes.Take(dropped).Select(n => $"{n.SpecId} ({n.DoneFile})"))).Append('\n');
        foreach (var block in blocks.Skip(dropped)) sb.Append(block);
        return sb.ToString();
    }

    private static string Block(PrecedingNote preceding)
    {
        var sb = new StringBuilder($"### {preceding.SpecId}\n");
        if (preceding.Note is not { } note) return sb.Append(NoNote).Append('\n').ToString();
        Lines(sb, "renamed", note.Renamed);
        Lines(sb, "moved", note.Moved);
        Lines(sb, "persisted", note.Persisted);
        Lines(sb, "deviation", note.Deviations);
        if (note.Renamed.Count + note.Moved.Count + note.Persisted.Count + note.Deviations.Count == 0)
            sb.Append("nothing renamed, moved, persisted or deviated from\n");
        return sb.ToString();
    }

    private static void Lines(StringBuilder sb, string label, IReadOnlyList<string> entries)
    {
        foreach (var entry in entries) sb.Append("- ").Append(label).Append(": ").Append(entry).Append('\n');
    }
}

/// <summary>2026-10-13-8762m: one spec that ran before the current one, its record's path in
/// <c>specs/done/</c>, and its build note (null: no note).</summary>
public sealed record PrecedingNote(string SpecId, string DoneFile, SpecBuildNote? Note);
