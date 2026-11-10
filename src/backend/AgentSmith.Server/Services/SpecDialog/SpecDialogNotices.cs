using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-ccd9: a line the framework says in a conversation is both sent and kept as a Notice turn,
/// so a reload shows why a card went away — the same pairing a refused filing already uses.
/// </summary>
public sealed class SpecDialogNotices(SpecDialogMessenger messenger, SpecDialogSessionManager sessions)
{
    /// <param name="runId">2026-11-10-d854c: the run of the turn the notice is about, kept on the Notice turn.</param>
    public async Task TellAsync(ConversationState state, ComposedReply notice, CancellationToken ct, string? runId = null)
    {
        var text = notice.In(SpecDialogMarkup.For(state.Platform));
        await messenger.SendAsync(state.Platform, state.ChannelId, state.ThreadId!, text, ct);
        await KeepAsync(state, text, ct, runId);
    }

    /// <summary>2026-11-10-d854c: keeps the Notice turn without sending it — a cancelled turn's page re-reads it.</summary>
    public Task KeepAsync(ConversationState state, string text, CancellationToken ct, string? runId = null) =>
        sessions.AppendTurnAsync(
            state.Platform, state.ThreadId!, TranscriptRole.Assistant, text, SpecDialogTurnKind.Notice, null, ct, runId: runId);
}
