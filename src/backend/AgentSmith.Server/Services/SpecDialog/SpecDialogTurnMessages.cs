using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Turns;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-12-a1fa: one turn's view of the operator inbox — its poster's messages, drained into the turn's capture as
/// handed over, so the thread written after the next successful call records them delivered.
/// </summary>
public sealed class SpecDialogTurnMessages(
    SpecDialogOperatorInbox inbox, SpecDialogTurnGate gate, string sessionId, TurnPoster poster,
    ForwardedThreadCapture capture) : IMidTurnMessages
{
    public void Lead(bool stewarded) => gate.Lead(sessionId, stewarded);

    public IReadOnlyList<MidTurnMessage> Drain()
    {
        var drained = inbox.Drain(sessionId, poster);
        foreach (var message in drained) capture.Drained(message.Index);
        return [.. drained.Select(m => new MidTurnMessage(m.Text, m.At))];
    }
}
