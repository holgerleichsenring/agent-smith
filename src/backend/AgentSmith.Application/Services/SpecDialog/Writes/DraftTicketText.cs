using AgentSmith.Contracts.Models;
using YamlDotNet.Core;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-15-06fc: a ticket of the draft as its file <c>tickets/&lt;key&gt;.yaml</c> — title, requirement, done,
/// requires and repos as YAML — and back. The key is the file's name.
/// </summary>
public static class DraftTicketText
{
    public static string Render(GraphTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var map = new Dictionary<string, object>
        {
            ["title"] = ticket.Title, ["requirement"] = ticket.Requirement, ["done"] = ticket.Done,
        };
        if (ticket.Requires.Count > 0) map["requires"] = ticket.Requires;
        if (ticket.Repos.Count > 0) map["repos"] = ticket.Repos;
        return OutcomeYamlReader.ToYaml(map);
    }

    /// <summary>The ticket the file states, or why it cannot be read.</summary>
    public static (GraphTicket? Ticket, string? Error) Read(string key, string text)
    {
        IReadOnlyDictionary<string, object?> map;
        try
        {
            map = OutcomeYamlReader.ReadMap(text ?? string.Empty);
        }
        catch (YamlException ex)
        {
            return (null, $"ticket '{key}' is not a YAML mapping of title, requirement, done: {ex.Message}");
        }
        return (new GraphTicket(key, Trimmed(map, "title"), Trimmed(map, "requirement"),
            Lines(map, "done"), Lines(map, "requires"), Lines(map, "repos")), null);
    }

    private static string Trimmed(IReadOnlyDictionary<string, object?> map, string key) =>
        (OutcomeYamlReader.GetString(map, key) ?? string.Empty).Trim();

    private static IReadOnlyList<string> Lines(IReadOnlyDictionary<string, object?> map, string key) =>
        [.. SpecDraftLists.Strings(map, key).Select(line => line.Trim())];
}
