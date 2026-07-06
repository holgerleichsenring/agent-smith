using AgentSmith.Contracts.Models.Triggers;
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
         "System.State", "System.Tags", "Microsoft.VSTS.Common.AcceptanceCriteria",
         // p0318: a Bug's body lives here, not System.Description — hydrate it on the
         // list/poll path too (not just the single GetWorkItem fetch) so AzureDevOpsFieldMapper
         // can fall back to it and the planner receives the repro text.
         "Microsoft.VSTS.TCM.ReproSteps"];

    private const int MaxResults = 1000;   // WIQL id cap → no silent 50-id truncation
    private const int HydrateBatch = 200;  // AzDO GetWorkItemsAsync hard per-call limit

    private readonly IAzureDevOpsDiscoveryWiqlBuilder _whereBuilder = new AzureDevOpsDiscoveryWiqlBuilder();

    // Broad open discovery (ListOpenAsync + the lifecycle-tag queries via extraWhere).
    public Task<IReadOnlyList<Ticket>> ListAsync(
        string? extraWhere, string descriptor, CancellationToken cancellationToken)
    {
        var where = $"[System.TeamProject] = '{project}' AND [System.State] IN ({StatesList(OpenStates)})";
        if (!string.IsNullOrEmpty(extraWhere)) where += $" AND {extraWhere}";
        return RunAsync(where, descriptor, cancellationToken);
    }

    // p0283b: composed claimable discovery — the builder turns the DiscoveryQuery into the
    // per-project OR clause (status + tag/area-path); a broad branch excludes the parking statuses.
    public Task<IReadOnlyList<Ticket>> ListClaimableAsync(
        DiscoveryQuery query, CancellationToken cancellationToken)
    {
        var where = $"[System.TeamProject] = '{project}' AND ({_whereBuilder.BuildWhere(query, OpenStates)})";
        return RunAsync(where, "claimable", cancellationToken);
    }

    private IReadOnlyList<string> OpenStates => openStates is { Count: > 0 } ? openStates : DefaultOpenStates;

    private static string StatesList(IReadOnlyList<string> states) =>
        string.Join(", ", states.Select(s => $"'{s}'"));

    private async Task<IReadOnlyList<Ticket>> RunAsync(
        string where, string descriptor, CancellationToken cancellationToken)
    {
        logger.LogInformation("AzDO List: project={Project} {Descriptor}", project, descriptor);
        try
        {
            var tickets = await RunWiqlAsync(where, cancellationToken);
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

    private async Task<IReadOnlyList<Ticket>> RunWiqlAsync(string where, CancellationToken cancellationToken)
    {
        var client = connections.CreateClient();
        var wiql = new Wiql
        {
            Query = $"SELECT [System.Id] FROM WorkItems WHERE {where} ORDER BY [System.ChangedDate] DESC"
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        logger.LogDebug("AzDO WIQL query: {Query}", wiql.Query);
        var result = await client.QueryByWiqlAsync(wiql, project, top: MaxResults, cancellationToken: cancellationToken);
        logger.LogDebug("AzDO WIQL query completed in {Ms}ms, {Count} ids returned",
            sw.ElapsedMilliseconds, result.WorkItems?.Count() ?? 0);

        if (result.WorkItems is null || !result.WorkItems.Any()) return [];

        var ids = result.WorkItems.Select(w => w.Id).ToArray();
        if (ids.Length >= MaxResults)
            logger.LogWarning(
                "AzDO WIQL: hit {Max}-result cap — results truncated; narrow trigger_statuses "
                + "to shrink the candidate set", MaxResults);
        var fields = extraFields is { Count: > 0 }
            ? StandardFields.Union(extraFields).Distinct().ToArray()
            : StandardFields;

        // GetWorkItemsAsync caps at 200 ids/call — hydrate in batches so a raised
        // WIQL cap doesn't blow past the API limit.
        var tickets = new List<Ticket>(ids.Length);
        for (var offset = 0; offset < ids.Length; offset += HydrateBatch)
        {
            var batch = ids.Skip(offset).Take(HydrateBatch).ToArray();
            var workItems = await client.GetWorkItemsAsync(batch, fields: fields, cancellationToken: cancellationToken);
            tickets.AddRange(workItems
                .Where(w => w?.Fields is not null)
                .Select(w => mapper.Map(new TicketId(w.Id!.Value.ToString()), w.Fields)));
        }
        return tickets;
    }

    private static bool IsTransportFailure(Exception ex) => ex is
        System.Net.Http.HttpRequestException
        or System.Net.Sockets.SocketException
        or TaskCanceledException
        or VssServiceException;
}
