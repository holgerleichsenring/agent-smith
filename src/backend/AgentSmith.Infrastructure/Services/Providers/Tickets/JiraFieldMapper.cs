using System.Text.Json;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: maps a Jira Cloud REST v3 issue JSON object onto the canonical
/// <see cref="Ticket"/>. Jira returns its <c>fields</c> object as a child
/// of the issue (unlike GitLab's flat shape), so the mapper drills one
/// level deeper. Descriptions are ADF (Atlassian Document Format); plain
/// text is extracted via <see cref="JiraAdfParser"/>.
/// </summary>
public sealed class JiraFieldMapper : ITicketFieldMapper<JsonElement>
{
    public Ticket Map(TicketId ticketId, JsonElement issue)
    {
        var fields = issue.TryGetProperty("fields", out var f) ? f : issue;
        return new Ticket(
            ticketId,
            ReadString(fields, "summary"),
            ReadAdf(fields, "description"),
            acceptanceCriteria: null,
            ReadStatus(fields),
            "Jira",
            ReadStringArray(fields, "labels"));
    }

    /// <summary>
    /// Iterates the <c>issues</c> array of a Jira search response and maps
    /// each to a <see cref="Ticket"/>, keyed by the issue's <c>key</c>.
    /// Skips entries without a key.
    /// </summary>
    public IReadOnlyList<Ticket> MapSearchResponse(JsonElement root)
    {
        if (!root.TryGetProperty("issues", out var issuesEl)
            || issuesEl.ValueKind != JsonValueKind.Array) return [];
        var tickets = new List<Ticket>(issuesEl.GetArrayLength());
        foreach (var issue in issuesEl.EnumerateArray())
        {
            if (!issue.TryGetProperty("key", out var keyEl) || keyEl.GetString() is not { } key) continue;
            tickets.Add(Map(new TicketId(key), issue));
        }
        return tickets;
    }

    private static string ReadString(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Undefined
            ? ""
            : el.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null
                ? v.GetString() ?? ""
                : "";

    private static string ReadAdf(JsonElement el, string name) =>
        el.ValueKind != JsonValueKind.Undefined
            && el.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null
                ? JiraAdfParser.ExtractText(v)
                : "";

    private static string ReadStatus(JsonElement fields) =>
        fields.ValueKind != JsonValueKind.Undefined
            && fields.TryGetProperty("status", out var stEl)
            && stEl.TryGetProperty("name", out var nameEl)
                ? nameEl.GetString() ?? "" : "";

    private static IReadOnlyList<string> ReadStringArray(JsonElement el, string name)
    {
        if (el.ValueKind == JsonValueKind.Undefined
            || !el.TryGetProperty(name, out var arr)
            || arr.ValueKind != JsonValueKind.Array) return [];
        return arr.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString() ?? string.Empty)
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }
}
