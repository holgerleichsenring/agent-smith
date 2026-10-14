using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Keeps a design turn's reply in the conversation and names a conversation that has none, and, once the turn gate
/// is released, folds a conversation that has grown past its window into a digest. Both masters' turns are kept here.
/// </summary>
public sealed class SpecDialogTurnKeeper(
    SpecDialogSessionManager sessions, SpecDialogSubjectMinter subjects, SpecDialogDigestWriter digests)
{
    public async Task KeepAsync(
        ConversationState state, SpecDialogTurnResult result, string platform, string threadId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(result);
        await sessions.AppendTurnAsync(platform, threadId, TranscriptRole.Assistant, result.Reply, result.Kind, null, ct);
        // 2026-09-20-4b0af: between the two, and nowhere else. The trigger — a conversation
        // with no subject and no assistant turn yet — is only true once its first assistant
        // turn is persisted, and the reply the router sends next is what makes the page re-read the
        // conversation, so a subject stored after it would be a read too late for a
        // conversation that asks one question and never comes back.
        await subjects.MintAsync(state, result.Kind, result.Reply, ct);
    }

    /// <summary>Writes the digest that is due, if one is; it never throws but on the caller's cancellation.</summary>
    public Task FoldAsync(ConversationState state, CancellationToken ct) => digests.WriteAsync(state, ct);
}
