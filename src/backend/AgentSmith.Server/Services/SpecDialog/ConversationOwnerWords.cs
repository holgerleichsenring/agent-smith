using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-15-53d0: the operator's own messages of a conversation, oldest first, as an approval record carries them —
/// every person's turn verbatim with its author and time; the design partner's turns are not among them.
/// <para>2026-11-17-5701b: nor is the approval being filed. The word or click that approved a card is stored as a user
/// turn carrying the decision and the proposal it decides; it says "approve", not what the operator wants built.</para>
/// </summary>
public static class ConversationOwnerWords
{
    /// <param name="proposalId">The proposal being filed; null when the filing names none, which leaves out the last
    /// turn when it carries an approval.</param>
    public static IReadOnlyList<OwnerWord> Of(ConversationState state, string? proposalId = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var approval = ApprovalTurn(state.Transcript, proposalId);
        return [.. state.Transcript
            .Where(t => t.Role == TranscriptRole.User && !string.IsNullOrWhiteSpace(t.Text) && !ReferenceEquals(t, approval))
            .Select(t => new OwnerWord(string.IsNullOrWhiteSpace(t.Author) ? state.UserId : t.Author!, t.At, t.Text))];
    }

    // The latest user turn approving this proposal; without an id, the last turn when it approves something.
    private static TranscriptTurn? ApprovalTurn(IReadOnlyList<TranscriptTurn> transcript, string? proposalId) =>
        proposalId is not null
            ? transcript.LastOrDefault(t => t.Role == TranscriptRole.User && t.Decision == SpecDialogDecision.Approved
                && string.Equals(t.DecisionProposalId, proposalId, StringComparison.Ordinal))
            : transcript.Count > 0 && transcript[^1] is { Role: TranscriptRole.User, Decision: SpecDialogDecision.Approved } last
                ? last : null;
}
