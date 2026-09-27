using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: Azure DevOps WIQL query helper for the polling and discovery readers. Composes the
/// WHERE clause; <see cref="AzureDevOpsWiqlRunner"/> runs it and hydrates the fields.
/// <para>
/// 2026-09-27-5c1ea: the running moved out, and with it the decision this class keeps making —
/// a failed query is answered with an empty list, because its callers poll and an empty cycle is
/// recoverable. The ticket search reuses the same runner and answers a failure as a refusal, which
/// is why the two could not share one method.
/// </para>
/// </summary>
internal sealed class AzureDevOpsWorkItemLister(
    AzureDevOpsConnectionCache connections,
    AzureDevOpsFieldMapper mapper,
    string project,
    IReadOnlyList<string>? openStates,
    IReadOnlyList<string>? extraFields,
    ILogger logger)
{
    private const int MaxResults = 1000;   // WIQL id cap → no silent 50-id truncation

    private readonly IAzureDevOpsDiscoveryWiqlBuilder _whereBuilder = new AzureDevOpsDiscoveryWiqlBuilder();
    private readonly AzureDevOpsWiqlRunner _runner =
        new(connections, mapper, project, extraFields, logger);

    // Broad open discovery (ListOpenAsync + the lifecycle-tag queries via extraWhere).
    public Task<IReadOnlyList<Ticket>> ListAsync(
        string? extraWhere, string descriptor, CancellationToken cancellationToken)
    {
        var where = AzureDevOpsOpenScope.Where(project, openStates);
        if (!string.IsNullOrEmpty(extraWhere)) where += $" AND {extraWhere}";
        return RunAsync(where, descriptor, cancellationToken);
    }

    // p0283b: composed claimable discovery — the builder turns the DiscoveryQuery into the
    // per-project OR clause (status + tag/area-path); a broad branch excludes the parking statuses.
    public Task<IReadOnlyList<Ticket>> ListClaimableAsync(
        DiscoveryQuery query, CancellationToken cancellationToken)
    {
        var states = AzureDevOpsOpenScope.States(openStates);
        var where = $"[System.TeamProject] = '{AzureDevOpsOpenScope.Escaped(project)}' "
            + $"AND ({_whereBuilder.BuildWhere(query, states)})";
        return RunAsync(where, "claimable", cancellationToken);
    }

    private async Task<IReadOnlyList<Ticket>> RunAsync(
        string where, string descriptor, CancellationToken cancellationToken)
    {
        logger.LogInformation("AzDO List: project={Project} {Descriptor}", project, descriptor);
        try
        {
            var tickets = await _runner.RunAsync(where, MaxResults, cancellationToken);
            logger.LogInformation("AzDO List: returned {Count} ticket(s)", tickets.Count);
            return tickets;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AzDO List failed for project={Project} {Descriptor}", project, descriptor);
            return [];
        }
    }
}
