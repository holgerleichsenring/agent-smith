using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.Common;
using Wiql = Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.Wiql;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-27-5c1ea: runs one WIQL query and hydrates the fields — extracted from
/// <see cref="AzureDevOpsWorkItemLister"/> so that the two readers can differ on what a FAILURE
/// means. The lister's callers poll, and for them a swallowed failure is a skipped cycle; a
/// person's search cannot be answered with an empty board, so it needs the exception.
/// <para>
/// A transport failure still evicts the cached connection on its way out, because that is a fact
/// about the connection rather than about either caller.
/// </para>
/// </summary>
internal sealed class AzureDevOpsWiqlRunner(
    AzureDevOpsConnectionCache connections,
    AzureDevOpsFieldMapper mapper,
    string project,
    IReadOnlyList<string>? extraFields,
    ILogger logger)
{
    private static readonly string[] StandardFields =
        ["System.Id", "System.Title", "System.Description",
         // 2026-09-27-481bf: what the process template calls this item. The WIQL SELECT asks only
         // for ids; this list is what the hydration fetches, and it serves the poll lister too.
         "System.WorkItemType",
         "System.State", "System.Tags", "Microsoft.VSTS.Common.AcceptanceCriteria",
         // p0318: a Bug's body lives here, not System.Description — hydrate it on the
         // list/poll path too (not just the single GetWorkItem fetch) so AzureDevOpsFieldMapper
         // can fall back to it and the planner receives the repro text.
         "Microsoft.VSTS.TCM.ReproSteps"];

    private const int HydrateBatch = 200;  // AzDO GetWorkItemsAsync hard per-call limit

    /// <summary>
    /// The work items matching <paramref name="where"/>, most recently changed first, at most
    /// <paramref name="top"/> of them. Throws when the query does not run.
    /// </summary>
    public async Task<IReadOnlyList<Ticket>> RunAsync(
        string where, int top, CancellationToken cancellationToken, string? orderBy = null)
    {
        try
        {
            return await QueryAsync(where, top, orderBy ?? ChangedDateDesc, cancellationToken);
        }
        catch (Exception ex)
        {
            if (IsTransportFailure(ex)) connections.Invalidate(ex);
            throw;
        }
    }

    /// <summary>2026-09-28-1da5a: discovery wants the most recently changed; a number-prefix
    /// search wants the ids nearest what was typed, so the ordering is the caller's.</summary>
    internal const string ChangedDateDesc = "[System.ChangedDate] DESC";

    private async Task<IReadOnlyList<Ticket>> QueryAsync(
        string where, int top, string orderBy, CancellationToken cancellationToken)
    {
        var client = connections.CreateClient();
        var wiql = new Wiql
        {
            Query = $"SELECT [System.Id] FROM WorkItems WHERE {where} ORDER BY {orderBy}"
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        logger.LogDebug("AzDO WIQL query: {Query}", wiql.Query);
        var result = await client.QueryByWiqlAsync(wiql, project, top: top, cancellationToken: cancellationToken);
        logger.LogDebug("AzDO WIQL query completed in {Ms}ms, {Count} ids returned",
            sw.ElapsedMilliseconds, result.WorkItems?.Count() ?? 0);

        if (result.WorkItems is null || !result.WorkItems.Any()) return [];

        var ids = result.WorkItems.Select(w => w.Id).ToArray();
        if (ids.Length >= top)
            logger.LogWarning(
                "AzDO WIQL: hit {Max}-result cap — results truncated; narrow trigger_statuses "
                + "to shrink the candidate set", top);
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
