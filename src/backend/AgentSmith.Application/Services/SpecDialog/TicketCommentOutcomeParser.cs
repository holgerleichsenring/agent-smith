using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-10-28-7a69c: parses a `kind: comment` outcome block — the comment a ticket's conversation
/// proposes to post on it. One required field, trimmed, bounded so a ticket gets a comment and not
/// a transcript.
/// </summary>
public sealed class TicketCommentOutcomeParser
{
    /// <summary>The longest comment accepted; the skill states the same number.</summary>
    public const int MaxLength = 4_000;

    public OutcomeResolution Parse(IReadOnlyDictionary<string, object?> map)
    {
        var comment = OutcomeYamlReader.GetString(map, "comment")?.Trim();
        if (string.IsNullOrEmpty(comment))
            return new OutcomeInvalid("comment outcome is missing 'comment'");
        return comment.Length > MaxLength
            ? new OutcomeInvalid($"comment outcome's 'comment' is {comment.Length} characters — at most {MaxLength}")
            : new OutcomeResolved(new TicketCommentOutcome(comment));
    }
}
