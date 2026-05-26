using System.Text.Json;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: maps a GitLab REST v4 issue JSON object onto the canonical
/// <see cref="Ticket"/>. Stateless. Tolerant of missing fields (null
/// description is collapsed to empty string, missing labels to empty list).
/// </summary>
public sealed class GitLabFieldMapper : ITicketFieldMapper<JsonElement>
{
    public Ticket Map(TicketId ticketId, JsonElement issue) =>
        new(
            ticketId,
            ReadString(issue, "title"),
            ReadString(issue, "description"),
            acceptanceCriteria: null,
            ReadString(issue, "state"),
            "GitLab",
            ReadStringArray(issue, "labels"));

    /// <summary>
    /// Maps an array of GitLab issues. Filters out entries without a valid
    /// <c>iid</c> (the GitLab issue id used as TicketId).
    /// </summary>
    public IReadOnlyList<Ticket> MapMany(JsonElement issuesArray)
    {
        if (issuesArray.ValueKind != JsonValueKind.Array) return [];
        var tickets = new List<Ticket>(issuesArray.GetArrayLength());
        foreach (var issue in issuesArray.EnumerateArray())
        {
            if (!issue.TryGetProperty("iid", out var iidEl)) continue;
            tickets.Add(Map(new TicketId(iidEl.GetInt64().ToString()), issue));
        }
        return tickets;
    }

    private static string ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null
            ? v.GetString() ?? "" : "";

    private static IReadOnlyList<string> ReadStringArray(JsonElement el, string name) =>
        el.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array
            ? arr.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString() ?? string.Empty)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList()
            : [];
}
