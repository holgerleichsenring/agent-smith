using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Rework;

namespace AgentSmith.Server.Contracts;

/// <summary>2026-10-13-8762v: continues a ticket's spec series once an attempt ended with no person's act waiting.</summary>
public interface ISeriesAutoStart
{
    Task<ReworkNudgeDisposition> ContinueAsync(ResolvedProject project, string ticketId, CancellationToken ct);
}
