using System.Text.Json;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0317: maps a Jira Cloud REST v3 <c>/issue/{key}/comment</c> response onto the
/// canonical <see cref="TicketComment"/>. Comment bodies arrive as ADF and are
/// flattened to plain text via <see cref="JiraAdfParser"/>. Stateless and tolerant
/// of missing fields, mirroring <see cref="JiraFieldMapper"/>.
/// </summary>
public sealed class JiraCommentMapper
{
    public IReadOnlyList<TicketComment> MapMany(JsonElement root)
    {
        if (!root.TryGetProperty("comments", out var comments)
            || comments.ValueKind != JsonValueKind.Array)
            return [];
        return comments.EnumerateArray().Select(Map).ToList();
    }

    private static TicketComment Map(JsonElement comment)
    {
        var author = comment.TryGetProperty("author", out var a)
            && a.ValueKind == JsonValueKind.Object
            && a.TryGetProperty("displayName", out var dn)
            && dn.ValueKind == JsonValueKind.String
            ? dn.GetString() ?? "unknown"
            : "unknown";
        var createdAt = comment.TryGetProperty("created", out var c)
            && c.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(c.GetString(), out var ts)
            ? ts
            : DateTimeOffset.MinValue;
        var body = comment.TryGetProperty("body", out var b)
            ? JiraAdfParser.ExtractText(b)
            : string.Empty;
        return new TicketComment(author, createdAt, body);
    }
}
