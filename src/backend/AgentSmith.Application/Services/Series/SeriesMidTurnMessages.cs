using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Turns;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207b: what the operator writes while the series master works reaches it with its next tool
/// result, read from the stored conversation past the beat's cursor.</summary>
public sealed class SeriesMidTurnMessages(SeriesBeat beat, ISeriesConversation conversation) : IMidTurnMessages
{
    public void Lead(bool stewarded) { }

    public async ValueTask<IReadOnlyList<MidTurnMessage>> DrainAsync(CancellationToken cancellationToken)
    {
        var waiting = await conversation.AddressedAsync(
            beat.Record.Conversation, beat.Record.Id, beat.MessagesThrough, cancellationToken);
        if (waiting.Count == 0) return [];
        beat.MessagesThrough = waiting[^1].Index;
        return [.. waiting.Select(m => new MidTurnMessage(m.Text, m.At))];
    }
}
