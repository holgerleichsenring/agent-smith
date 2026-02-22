using AgentSmith.Dispatcher.Models;

namespace AgentSmith.Dispatcher.Contracts;

/// <summary>
/// Resolves which configured project contains a given ticket.
/// Queries ticket providers in parallel when multiple projects are configured.
/// </summary>
public interface IProjectResolver
{
    Task<ProjectResolverResult> ResolveAsync(
        string ticketNumber, CancellationToken cancellationToken = default);
}
