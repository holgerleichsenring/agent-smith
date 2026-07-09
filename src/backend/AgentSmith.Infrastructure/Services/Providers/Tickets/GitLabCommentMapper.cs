using System.Text.Json;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0317: maps a GitLab REST v4 <c>/issues/{iid}/notes</c> response onto the
/// canonical <see cref="TicketComment"/>. System notes ("changed label X",
/// state events) are tracker bookkeeping, not conversation — they are skipped.
/// Stateless and tolerant of missing fields, mirroring <see cref="GitLabFieldMapper"/>.
/// </summary>
public sealed class GitLabCommentMapper
{
    public IReadOnlyList<TicketComment> MapMany(JsonElement notesArray)
    {
        if (notesArray.ValueKind != JsonValueKind.Array) return [];
        var comments = new List<TicketComment>(notesArray.GetArrayLength());
        foreach (var note in notesArray.EnumerateArray())
        {
            if (note.TryGetProperty("system", out var sys) && sys.ValueKind == JsonValueKind.True)
                continue;
            comments.Add(Map(note));
        }
        return comments;
    }

    private static TicketComment Map(JsonElement note)
    {
        var author = note.TryGetProperty("author", out var a) && a.ValueKind == JsonValueKind.Object
            ? ReadString(a, "name") is { Length: > 0 } name ? name : ReadString(a, "username")
            : string.Empty;
        var createdAt = DateTimeOffset.TryParse(ReadString(note, "created_at"), out var ts)
            ? ts
            : DateTimeOffset.MinValue;
        return new TicketComment(
            author.Length > 0 ? author : "unknown", createdAt, ReadString(note, "body"));
    }

    private static string ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? string.Empty
            : string.Empty;
}
