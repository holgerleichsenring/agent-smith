using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Pocs;

/// <summary>
/// 2026-11-23-933da: the PoCs page's answer. The rows are those of the projects the caller reads runs in
/// (<see cref="CallerProjectScope"/>: every project when nothing is enforced, a global reader all but the hidden
/// installation, a poc- binding its own project), read from one configuration load.
/// </summary>
internal sealed class PocListReader(
    CallerProjectScope scopes, ProjectAccess access, PocRepository pocs, TicketProjects projects)
{
    private static readonly string[] Write = [Permissions.PocsWrite];

    public async Task<PocListAnswer> ReadAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var config = projects.Config();
        var scope = scopes.For(user, Permissions.RunsRead, config.Projects.Keys);
        var rows = await pocs.ListAsync(scope, ct);
        var latest = await pocs.LatestRevisionsAsync([.. rows.Select(p => p.Id)], ct);
        var mayImport = !access.Enforcing || access.HoldsEverywhere(user, Write);
        return new PocListAnswer(
            [.. rows.Select(poc => Item(poc, config.Pocs) with { LatestRevision = latest.TryGetValue(poc.Id, out var n) ? n : null })], mayImport,
            [.. config.Pocs.Targets.Select(t => new PocTargetView(t.Name, t.SourceRepo, t.Root))]);
    }

    private static PocListItem Item(Poc poc, PocsConfig pocs)
    {
        var pattern = pocs.Targets.FirstOrDefault(t => string.Equals(t.Name, poc.Target, StringComparison.OrdinalIgnoreCase))
            ?.DomainPattern;
        var host = string.IsNullOrWhiteSpace(pattern) ? null : pattern.Replace("{app}", poc.Name, StringComparison.Ordinal);
        return new PocListItem(poc.Id, poc.Name, poc.Project, poc.Target, host, poc.State, poc.OwnerName, poc.LastRunId);
    }
}
