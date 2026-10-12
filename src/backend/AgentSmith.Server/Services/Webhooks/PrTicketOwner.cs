using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Equal repositories only, and of those the projects that actually ran this ticket: issue numbers are
/// per repository, so two projects on one repository could both own a #12. None or several owners
/// is no owner.
/// </summary>
public sealed class PrTicketOwner(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    IConfiguredRepoFinder repoFinder,
    IPreviousAttemptReader attempts,
    ILogger<PrTicketOwner> logger) : IPrTicketOwner
{
    public async Task<ConfiguredRepo?> FindAsync(
        string repoUrl, string ticketId, IReadOnlySet<string>? allowedProjects, CancellationToken cancellationToken)
    {
        var owners = new List<ConfiguredRepo>();
        foreach (var candidate in repoFinder.FindAll(configLoader.LoadConfig(serverContext.ConfigPath), repoUrl))
            if ((allowedProjects is null || allowedProjects.Contains(candidate.ProjectName))
                && await attempts.LatestAsync(candidate.ProjectName, ticketId, null, cancellationToken) is not null)
                owners.Add(candidate);
        if (owners.Count != 1)
            logger.LogInformation("Pull request on {Repo} for ticket {Ticket}: {Count} owning project(s) — not handled",
                repoUrl, ticketId, owners.Count);
        return owners.Count == 1 ? owners[0] : null;
    }
}
