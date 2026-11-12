using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-28-7a69c: parses a `kind: comment` outcome block — the comment a ticket's conversation
/// proposes to post on it. One required field, trimmed; 2026-11-12-cd4cd: no length bound.
/// </summary>
public sealed class TicketCommentOutcomeParser
{
    public OutcomeResolution Parse(IReadOnlyDictionary<string, object?> map)
    {
        var comment = OutcomeYamlReader.GetString(map, "comment")?.Trim();
        if (string.IsNullOrEmpty(comment))
            return new OutcomeInvalid("comment outcome is missing 'comment'");
        return new OutcomeResolved(new TicketCommentOutcome(comment));
    }
}
