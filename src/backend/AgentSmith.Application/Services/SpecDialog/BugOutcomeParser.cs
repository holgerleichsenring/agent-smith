using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315e: parses a `kind: bug` outcome block into the fix-bug ticket shape.
/// Title and description are the fields the existing fix-bug pipeline reads
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

        var acceptanceCriteria = OutcomeYamlReader.GetString(map, "acceptance_criteria");
        return new OutcomeResolved(new BugOutcome(
            new BugTicketDraft(title.Trim(), description.Trim(), acceptanceCriteria?.Trim())));
    }
}
