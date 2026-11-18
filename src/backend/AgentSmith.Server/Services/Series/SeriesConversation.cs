using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-17-0207a: the Work it out conversation a series reports into — a kept notice turn, pushed to the
/// page like every agent message there, and the operator's messages the divert addressed to the series.</summary>
public sealed class SeriesConversation(SpecDialogSessionRepository sessions, SpecDialogNotices notices) : ISeriesConversation
{
    public async Task SayAsync(string conversation, string text, CancellationToken cancellationToken)
    {
        var session = await sessions.GetBySessionIdAsync(conversation, cancellationToken);
        if (session is null || !session.IsOpen) return;
        await notices.SayAsync(SpecDialogSessionMapper.ToState(session), text, cancellationToken);
    }

    /// <summary>2026-11-17-0207b: the operator turns the divert stamped with the series' id, from the stored transcript,
    /// so a beat reads them on whichever replica it runs.</summary>
    public async Task<IReadOnlyList<SeriesOperatorMessage>> AddressedAsync(
        string conversation, string seriesRunId, int afterIndex, CancellationToken cancellationToken)
    {
        var session = await sessions.GetBySessionIdAsync(conversation, cancellationToken);
        if (session is null) return [];
        return [.. SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson).Select((turn, index) => (turn, index))
            .Where(t => t.index > afterIndex && t.turn.Role == TranscriptRole.User
                && string.Equals(t.turn.SeriesRunId, seriesRunId, StringComparison.Ordinal))
            .Select(t => new SeriesOperatorMessage(t.index, t.turn.Text, t.turn.At))];
    }
}
