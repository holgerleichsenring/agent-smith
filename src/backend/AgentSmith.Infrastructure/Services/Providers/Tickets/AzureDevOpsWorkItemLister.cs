using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Common;
using Wiql = Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.Wiql;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: Azure DevOps WIQL query helper. Builds the SELECT, runs it, then
/// hydrates fields via a second GetWorkItemsAsync call (the WIQL response
/// only carries ids by API contract). Wraps the call in transport-failure
/// recovery: a sporadic 503 evicts the cached <see cref="VssConnection"/>.
/// </summary>
internal sealed class AzureDevOpsWorkItemLister(
    AzureDevOpsConnectionCache connections,
    AzureDevOpsFieldMapper mapper,
    string project,
    IReadOnlyList<string>? openStates,
    IReadOnlyList<string>? extraFields,
    ILogger logger)
{
    private static readonly string[] DefaultOpenStates = ["New", "Active", "Committed"];
    private static readonly string[] StandardFields =
        ["System.Id", "System.Title", "System.Description",
         "System.State", "System.Tags", "Microsoft.VSTS.Common.AcceptanceCriteria"];

    public async Task<IReadOnlyList<Ticket>> ListAsync(
        string? extraWhere, string descriptor, CancellationToken cancellationToken)
    {
        logger.LogInformation("AzDO List: project={Project} {Descriptor}", project, descriptor);
        try
        {
            var tickets = await RunWiqlAsync(extraWhere, cancellationToken);
            logger.LogInformation("AzDO List: returned {Count} ticket(s)", tickets.Count);
            return tickets;
        }
        catch (Exception ex)
        {
            if (IsTransportFailure(ex)) connections.Invalidate(ex);
            logger.LogWarning(ex, "AzDO List failed for project={Project} {Descriptor}", project, descriptor);
            return [];
        }
    }

    private async Task<IReadOnlyList<Ticket>> RunWiqlAsync(
        string? extraWhere, CancellationToken cancellationToken)
    {
        var client = connections.CreateClient();
        var states = openStates is { Count: > 0 } ? openStates : DefaultOpenStates;
        var stateFilter = string.Join(", ", states.Select(s => $"'{s}'"));
        var where = $"[System.TeamProject] = '{project}' AND [System.State] IN ({stateFilter})";
        if (!string.IsNullOrEmpty(extraWhere)) where += $" AND {extraWhere}";

        var wiql = new Wiql
        {
            Query = $"SELECT [System.Id] FROM WorkItems WHERE {where} ORDER BY [System.ChangedDate] DESC"
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        logger.LogDebug("AzDO WIQL query: {Where}", where);
        var result = await client.QueryByWiqlAsync(wiql, project, top: 50, cancellationToken: cancellationToken);
        logger.LogDebug("AzDO WIQL query completed in {Ms}ms, {Count} ids returned",
            sw.ElapsedMilliseconds, result.WorkItems?.Count() ?? 0);

        if (result.WorkItems is null || !result.WorkItems.Any()) return [];

        var ids = result.WorkItems.Select(w => w.Id).ToArray();
        var fields = extraFields is { Count: > 0 }
            ? StandardFields.Union(extraFields).Distinct().ToArray()
            : StandardFields;

        var workItems = await client.GetWorkItemsAsync(ids, fields: fields, cancellationToken: cancellationToken);
        return workItems
            .Where(w => w?.Fields is not null)
            .Select(w => mapper.Map(new TicketId(w.Id!.Value.ToString()), w.Fields))
            .ToList();
    }

    private static bool IsTransportFailure(Exception ex) => ex is
        System.Net.Http.HttpRequestException
        or System.Net.Sockets.SocketException
        or TaskCanceledException
        or VssServiceException;
}
