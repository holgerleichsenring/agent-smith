using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-17-0207a: the Work it out conversation a series reports into — a kept notice turn, pushed to the
/// page like every agent message there.</summary>
public sealed class SeriesConversation(SpecDialogSessionRepository sessions, SpecDialogNotices notices) : ISeriesConversation
{
    public async Task SayAsync(string conversation, string text, CancellationToken cancellationToken)
    {
        var session = await sessions.GetBySessionIdAsync(conversation, cancellationToken);
        if (session is null || !session.IsOpen) return;
        await notices.SayAsync(SpecDialogSessionMapper.ToState(session), text, cancellationToken);
    }
}
