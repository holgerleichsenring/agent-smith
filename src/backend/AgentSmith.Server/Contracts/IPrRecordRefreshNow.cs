using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Contracts;

/// <summary>2026-11-24-a4e7: reads a ticket's pull requests from their hosts into their records now, for an act that must
/// answer with what the host says after it changed something. It never throws and takes no token: the act already happened.</summary>
public interface IPrRecordRefreshNow
{
    Task RefreshAsync(ResolvedProject project, string ticketId);
}
