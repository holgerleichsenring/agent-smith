using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-ccd9: a line the framework says in a conversation is both kept as a Notice turn and sent,
/// so a reload shows why a card went away — the same pairing a refused filing already uses. It is KEPT FIRST:
/// a send that fails must not take the record with it.
/// </summary>
public sealed class SpecDialogNotices(SpecDialogMessenger messenger, SpecDialogSessionManager sessions)
{
    /// <param name="runId">2026-11-10-d854c: the run of the turn the notice is about, kept on the Notice turn.</param>
    public Task TellAsync(ConversationState state, ComposedReply notice, CancellationToken ct, string? runId = null) =>
        SayAsync(state, notice.In(SpecDialogMarkup.For(state.Platform)), ct, runId);

    /// <summary>A line already in the platform's dialect, kept and then sent.</summary>
    public async Task SayAsync(ConversationState state, string text, CancellationToken ct, string? runId = null)
    {
        await KeepAsync(state, text, ct, runId);
        await messenger.SendAsync(state.Platform, state.ChannelId, state.ThreadId!, text, ct);
    }

    /// <summary>2026-11-10-d854c: keeps the Notice turn without sending it — a cancelled turn's page re-reads it.</summary>
    public Task KeepAsync(ConversationState state, string text, CancellationToken ct, string? runId = null) =>
        sessions.AppendTurnAsync(
            state.Platform, state.ThreadId!, TranscriptRole.Assistant, text, SpecDialogTurnKind.Notice, null, ct, runId: runId);
}
