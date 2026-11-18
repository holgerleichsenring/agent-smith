using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-17-0207e: the series of the conversation open on a dashboard dialog, as its panel shows it; null
/// when the conversation never filed one.</summary>
public sealed class SeriesRunViewReader(
    SpecDialogSessionRepository sessions, ISeriesRunStore store, ISeriesTicketFacts facts, TicketProjects projects,
    AgentSmithConfig config, TimeProvider clock)
{
    public async Task<SeriesRunView?> ReadAsync(string dialogId, CancellationToken ct)
    {
        var session = await sessions.GetOpenByThreadAsync(DispatcherDefaults.PlatformDashboard, dialogId, ct);
        if (session is null || await store.LatestForConversationAsync(session.SessionId, ct) is not { } record) return null;
        if (projects.Of(record.Project) is not { } project) return null;
        var read = await facts.ReadAsync(record.Project, record.Tickets, ct);
        return SeriesPanel.Of(record, read, clock.GetUtcNow(), project, config.Dialogue);
    }
}
