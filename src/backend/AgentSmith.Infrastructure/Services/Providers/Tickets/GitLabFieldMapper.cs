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
            ReadStringArray(issue, "labels"),
            ReadPerson(issue, "assignee"),
            ReadPerson(issue, "author"),
            // 2026-09-27-481bf: GitLab carries an issue type under TWO names across versions —
            // the older lowercase one and a newer uppercase enum — and a self-managed instance
            // below the version that added the second returns neither. Whichever is present, and
            // never rendered raw: an enum token is not a word a person recognises.
            Kind(issue));

    private static string? Kind(JsonElement issue) =>
        ReadOrNull(issue, "issue_type") ?? Spoken(ReadOrNull(issue, "type"));

    /// <summary>An uppercase enum token as the word it stands for: TEST_CASE reads "Test case".</summary>
    private static string? Spoken(string? token) =>
        string.IsNullOrWhiteSpace(token)
            ? null
            : char.ToUpperInvariant(token[0]) + token[1..].ToLowerInvariant().Replace('_', ' ');

    private static string? ReadOrNull(JsonElement issue, string name) =>
        issue.TryGetProperty(name, out var held) && held.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(held.GetString())
            ? held.GetString()
            : null;

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

    // p0454: GitLab mentions by @username; the display name is only what a reader sees.
    private static TicketPerson? ReadPerson(JsonElement issue, string name) =>
        issue.TryGetProperty(name, out var person) && person.ValueKind == JsonValueKind.Object
            ? TicketPerson.From(
                ReadString(person, "name") is { Length: > 0 } display
                    ? display : ReadString(person, "username"),
                ReadString(person, "username"))
            : null;

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
