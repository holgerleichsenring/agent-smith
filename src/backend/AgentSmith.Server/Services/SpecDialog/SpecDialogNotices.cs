using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-ccd9: a line the framework says in a conversation is both sent and kept as a Notice turn,
/// so a reload shows why a card went away — the same pairing a refused filing already uses.
/// </summary>
public sealed class SpecDialogNotices(SpecDialogMessenger messenger, SpecDialogSessionManager sessions)
{
    public async Task TellAsync(ConversationState state, ComposedReply notice, CancellationToken ct)
    {
        var text = notice.In(SpecDialogMarkup.For(state.Platform));
        await messenger.SendAsync(state.Platform, state.ChannelId, state.ThreadId!, text, ct);
        await sessions.AppendTurnAsync(
            state.Platform, state.ThreadId!, TranscriptRole.Assistant, text, SpecDialogTurnKind.Notice, null, ct);
    }
}
