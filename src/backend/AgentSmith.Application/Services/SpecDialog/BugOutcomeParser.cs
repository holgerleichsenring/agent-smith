using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315e: parses a `kind: bug` outcome block into the fix-bug ticket shape.
/// Title and description are the fields the existing code pipeline reads
/// off a ticket, so both are required; missing fields fail with their name.
/// </summary>
public sealed class BugOutcomeParser
{
    public OutcomeResolution Parse(IReadOnlyDictionary<string, object?> map)
    {
        var title = OutcomeYamlReader.GetString(map, "title");
        if (string.IsNullOrWhiteSpace(title))
            return new OutcomeInvalid("bug outcome is missing 'title'");

        var description = OutcomeYamlReader.GetString(map, "description");
        if (string.IsNullOrWhiteSpace(description))
            return new OutcomeInvalid("bug outcome is missing 'description'");

        return new OutcomeResolved(new BugOutcome(
            new BugTicketDraft(title.Trim(), description.Trim(), AcceptanceCriteria(map))));
    }

    // 2026-10-01-f5c3b: a string or a list. A list used to vanish here — GetString yields null
    // for anything not a string — so each item now becomes one listed line the section reader
    // takes as one criterion.
    private static string? AcceptanceCriteria(IReadOnlyDictionary<string, object?> map) =>
        map.TryGetValue("acceptance_criteria", out var value) ? value switch
        {
            string text => NullIfEmpty(text.Trim()),
            List<object?> items => NullIfEmpty(string.Join("\n", items
                .Select(item => CriterionLine.Collapse(DoneCriterion.Line(item)))
                .Where(line => line.Length > 0)
                .Select(line => $"- {line}"))),
            _ => null,
        } : null;

    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;
}
