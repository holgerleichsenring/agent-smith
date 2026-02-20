using AgentSmith.Contracts.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Services;

/// <summary>
/// Resolves which configured project contains a given ticket by querying
/// all ticket providers in parallel. Zero AI cost — pure API lookups.
/// </summary>
public sealed class ProjectResolver(
    IConfigurationLoader configLoader,
    ITicketProviderFactory ticketFactory,
    ILogger<ProjectResolver> logger) : IProjectResolver
{
    public async Task<ProjectResolverResult> ResolveAsync(
        string ticketNumber, CancellationToken cancellationToken = default)
    {
        var config = configLoader.LoadConfig(DispatcherDefaults.ConfigPath);
        var projects = config.Projects;

        if (projects.Count == 0)
            return new ProjectNotFound(ticketNumber);

        if (projects.Count == 1)
            return new ProjectResolved(projects.Keys.First());

        var matches = await FindMatchingProjectsAsync(projects, ticketNumber, cancellationToken);

        return matches.Count switch
        {
            0 => new ProjectNotFound(ticketNumber),
            1 => new ProjectResolved(matches[0]),
            _ => new ProjectAmbiguous(ticketNumber, matches)
        };
    }

    private async Task<List<string>> FindMatchingProjectsAsync(
        Dictionary<string, ProjectConfig> projects,
        string ticketNumber,
        CancellationToken cancellationToken)
    {
        var tasks = projects.Select(kvp =>
            CheckTicketExistsAsync(kvp.Key, kvp.Value.Tickets, ticketNumber, cancellationToken));

        var results = await Task.WhenAll(tasks);
        return results.Where(r => r is not null).Select(r => r!).ToList();
    }

    private async Task<string?> CheckTicketExistsAsync(
        string projectName,
        TicketConfig ticketConfig,
        string ticketNumber,
        CancellationToken cancellationToken)
    {
        try
        {
            var provider = ticketFactory.Create(ticketConfig);
            await provider.GetTicketAsync(new TicketId(ticketNumber), cancellationToken);
            return projectName;
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex, "Ticket #{TicketNumber} not found in {Project}", ticketNumber, projectName);
            return null;
        }
    }
}
