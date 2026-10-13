using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59h: a ticket's project as the configuration reads NOW — its tracker connection is what
/// the live thread, the between-attempts check and the bound conversation are found through.
/// </summary>
public sealed class TicketProjects(IConfigurationLoader configLoader, ServerContext serverContext)
{
    public ResolvedProject? Of(string project) =>
        configLoader.LoadConfig(serverContext.ConfigPath).Projects.TryGetValue(project, out var resolved) ? resolved : null;
}
