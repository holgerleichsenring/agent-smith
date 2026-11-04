using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// Keeps a design turn's reply in the conversation with the name the master gave it, and, once the turn gate is
/// released, folds a conversation that has grown past its window into a digest. Both masters' turns are kept here.
/// </summary>
public sealed class SpecDialogTurnKeeper(
    SpecDialogSessionManager sessions, SpecDialogSubjectStore subjects, SpecDialogDigestWriter digests)
{
    public async Task KeepAsync(
        ConversationState state, SpecDialogTurnResult result, string platform, string threadId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(result);
        await sessions.AppendTurnAsync(platform, threadId, TranscriptRole.Assistant, result.Reply, result.Kind, null, ct);
        // 2026-11-04-96a0a: before the router sends the reply, which is what makes the page re-read the conversation,
        // so a conversation that asks one question and never comes back is still headed with its name.
        if (state.Subject is null && result.Subject is { } subject && result.Kind != SpecDialogTurnKind.Failure)
            await subjects.StoreAsync(platform, threadId, subject, ct);
    }

    /// <summary>Writes the digest that is due, if one is; it never throws but on the caller's cancellation.</summary>
    public Task FoldAsync(ConversationState state, CancellationToken ct) => digests.WriteAsync(state, ct);
}
