using AgentSmith.Server.Services.Rework;

namespace AgentSmith.Server.Contracts;

/// <summary>2026-10-08-e8b9b: starts the attempt through the funnel a chat-named ticket run takes.</summary>
public interface IReworkLaunch
{
    Task<ReworkOutcome> LaunchAsync(string project, string ticketId, string pipeline, CancellationToken cancellationToken);
}
