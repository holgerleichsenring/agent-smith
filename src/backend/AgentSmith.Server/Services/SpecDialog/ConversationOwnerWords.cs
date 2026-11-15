using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-15-53d0: the operator's own messages of a conversation, oldest first, as an approval record carries them —
/// every person's turn verbatim with its author and time; the design partner's turns are not among them.
/// </summary>
public static class ConversationOwnerWords
{
    public static IReadOnlyList<OwnerWord> Of(ConversationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return [.. state.Transcript.Where(t => t.Role == TranscriptRole.User && !string.IsNullOrWhiteSpace(t.Text))
            .Select(t => new OwnerWord(string.IsNullOrWhiteSpace(t.Author) ? state.UserId : t.Author!, t.At, t.Text))];
    }
}
