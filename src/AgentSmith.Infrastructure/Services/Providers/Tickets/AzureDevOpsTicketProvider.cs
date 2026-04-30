using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Wiql = Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.Wiql;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Fetches work items from Azure DevOps.
/// </summary>
public sealed class AzureDevOpsTicketProvider(
    string organizationUrl,
    string project,
    string personalAccessToken,
    AzureDevOpsAttachmentLoader attachmentLoader,
    ILogger<AzureDevOpsTicketProvider> logger,
    IReadOnlyList<string>? openStates = null,
    string? doneStatus = null,
    IReadOnlyList<string>? extraFields = null) : ITicketProvider
{
    private static readonly string[] DefaultOpenStates = ["New", "Active", "Committed"];
    private static readonly string[] StandardFields =
        ["System.Id", "System.Title", "System.Description",
         "System.State", "System.Tags", "Microsoft.VSTS.Common.AcceptanceCriteria"];

    private readonly string _doneStatus = doneStatus ?? "Closed";

    public string ProviderType => "AzureDevOps";

    public async Task<Ticket> GetTicketAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var workItem = await FetchWorkItem(client, ticketId, cancellationToken);
        return MapToTicket(ticketId, workItem.Fields);
    }

    public async Task<IReadOnlyList<Ticket>> ListOpenAsync(
        CancellationToken cancellationToken)
    {
        return await ListWiqlAsync(extraWhere: null, cancellationToken);
    }

    public async Task<IReadOnlyList<Ticket>> ListByLifecycleStatusAsync(
        TicketLifecycleStatus status, CancellationToken cancellationToken)
    {
        var label = LifecycleLabels.For(status);
        logger.LogInformation(
            "AzDO ListByLifecycleStatus: project={Project} status={Status} (tag '{Label}')",
            project, status, label);
        try
        {
            var tickets = await ListWiqlAsync(extraWhere: $"[System.Tags] CONTAINS '{label}'", cancellationToken);
            logger.LogInformation(
                "AzDO ListByLifecycleStatus: returned {Count} ticket(s)", tickets.Count);
            return tickets;
        }
        catch (Exception ex)
        {
            if (IsTransportFailure(ex)) InvalidateConnection(ex);
            logger.LogWarning(ex,
                "AzDO ListByLifecycleStatus failed for project={Project} status={Status}",
                project, status);
            return [];
        }
    }

    public async Task<IReadOnlyList<Ticket>> ListByLabelsInOpenStatesAsync(
        IReadOnlyCollection<string> labels, CancellationToken cancellationToken)
    {
        if (labels.Count == 0) return [];

        logger.LogInformation(
            "AzDO ListByLabelsInOpenStates: project={Project} labels=[{Labels}]",
            project, string.Join(", ", labels));
        try
        {
            var tagOr = string.Join(" OR ",
                labels.Select(l => $"[System.Tags] CONTAINS '{EscapeWiql(l)}'"));
            var tickets = await ListWiqlAsync(extraWhere: $"({tagOr})", cancellationToken);
            logger.LogInformation(
                "AzDO ListByLabelsInOpenStates: returned {Count} ticket(s)", tickets.Count);
            return tickets;
        }
        catch (Exception ex)
        {
            if (IsTransportFailure(ex)) InvalidateConnection(ex);
            logger.LogWarning(ex,
                "AzDO ListByLabelsInOpenStates failed for project={Project} labels=[{Labels}]",
                project, string.Join(", ", labels));
            return [];
        }
    }

    private static string EscapeWiql(string s) => s.Replace("'", "''");

    private static bool IsTransportFailure(Exception ex) => ex is
        System.Net.Http.HttpRequestException
        or System.Net.Sockets.SocketException
        or TaskCanceledException
        or Microsoft.VisualStudio.Services.Common.VssServiceException;

    private async Task<IReadOnlyList<Ticket>> ListWiqlAsync(
        string? extraWhere, CancellationToken cancellationToken)
    {
        var client = CreateClient();

        var states = openStates is { Count: > 0 } ? openStates : DefaultOpenStates;
        var stateFilter = string.Join(", ", states.Select(s => $"'{s}'"));

        var whereClause = $"[System.TeamProject] = '{project}' AND [System.State] IN ({stateFilter})";
        if (!string.IsNullOrEmpty(extraWhere))
            whereClause += $" AND {extraWhere}";

        var wiql = new Wiql
        {
            Query = $"""
                SELECT [System.Id]
                FROM WorkItems
                WHERE {whereClause}
                ORDER BY [System.ChangedDate] DESC
                """
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        logger.LogDebug("AzDO WIQL query: {Where}", whereClause);
        var result = await client.QueryByWiqlAsync(wiql, project, top: 50,
            cancellationToken: cancellationToken);
        logger.LogDebug("AzDO WIQL query completed in {Ms}ms, {Count} ids returned",
            sw.ElapsedMilliseconds, result.WorkItems?.Count() ?? 0);

        if (result.WorkItems is null || !result.WorkItems.Any())
            return Array.Empty<Ticket>();

        var ids = result.WorkItems.Select(w => w.Id).ToArray();

        var fields = extraFields is { Count: > 0 }
            ? StandardFields.Union(extraFields).Distinct().ToArray()
            : StandardFields;

        var workItems = await client.GetWorkItemsAsync(
            ids,
            fields: fields,
            cancellationToken: cancellationToken);

        return workItems
            .Where(w => w?.Fields is not null)
            .Select(w =>
            {
                var id = new TicketId(w.Id!.Value.ToString());
                return MapToTicket(id, w.Fields);
            })
            .ToList();
    }

    private static readonly TimeSpan ConnectionTtl = TimeSpan.FromMinutes(30);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CachedConnection> _connectionCache = new();

    private sealed record CachedConnection(VssConnection Connection, DateTimeOffset CreatedAt)
    {
        public bool IsStale(TimeSpan ttl) => DateTimeOffset.UtcNow - CreatedAt > ttl;
    }

    private WorkItemTrackingHttpClient CreateClient()
    {
        var entry = _connectionCache.AddOrUpdate(
            organizationUrl,
            addValueFactory: url => Build(url),
            updateValueFactory: (url, existing) => existing.IsStale(ConnectionTtl)
                ? RebuildAfterTtl(url, existing)
                : existing);
        return entry.Connection.GetClient<WorkItemTrackingHttpClient>();
    }

    /// <summary>Evicts the cached connection so the next CreateClient rebuilds it.</summary>
    private void InvalidateConnection(Exception cause)
    {
        if (_connectionCache.TryRemove(organizationUrl, out _))
            logger.LogWarning(cause,
                "Evicting cached VssConnection for {Url} after transport failure: {Message}",
                organizationUrl, cause.Message);
    }

    private CachedConnection Build(string url)
    {
        logger.LogInformation("Initializing VssConnection for {Url}", url);
        var credentials = new VssBasicCredential(string.Empty, personalAccessToken);
        return new CachedConnection(new VssConnection(new Uri(url), credentials), DateTimeOffset.UtcNow);
    }

    private CachedConnection RebuildAfterTtl(string url, CachedConnection old)
    {
        logger.LogInformation("Refreshing VssConnection for {Url} (TTL {TtlMinutes}min reached)",
            url, ConnectionTtl.TotalMinutes);
        try { old.Connection.Dispose(); } catch { /* best-effort */ }
        return Build(url);
    }

    private async Task<Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem> FetchWorkItem(
        WorkItemTrackingHttpClient client, TicketId ticketId, CancellationToken ct)
    {
        if (!int.TryParse(ticketId.Value, out var id))
            throw new TicketNotFoundException(ticketId);

        var workItem = await client.GetWorkItemAsync(project, id, cancellationToken: ct);
        return workItem ?? throw new TicketNotFoundException(ticketId);
    }

    private static Ticket MapToTicket(
        TicketId ticketId, IDictionary<string, object> fields)
    {
        var tagsRaw = GetField(fields, "System.Tags");
        string[] labels = string.IsNullOrEmpty(tagsRaw)
            ? []
            : tagsRaw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new Ticket(
            ticketId,
            GetField(fields, "System.Title"),
            GetField(fields, "System.Description"),
            GetFieldOrNull(fields, "Microsoft.VSTS.Common.AcceptanceCriteria"),
            GetField(fields, "System.State"),
            "AzureDevOps",
            labels);
    }

    public async Task<IReadOnlyList<AttachmentRef>> GetAttachmentRefsAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(ticketId.Value, out var id))
            return [];

        try
        {
            return await attachmentLoader.GetRefsAsync(id, cancellationToken);
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<TicketImageAttachment>> DownloadImageAttachmentsAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        var refs = await GetAttachmentRefsAsync(ticketId, cancellationToken);
        if (refs.Count == 0) return [];

        var results = new List<TicketImageAttachment>();
        foreach (var r in refs)
        {
            var content = await attachmentLoader.DownloadAsync(r, cancellationToken);
            if (content is not null)
                results.Add(new TicketImageAttachment(r, content));
        }
        return results;
    }

    public async Task UpdateStatusAsync(
        TicketId ticketId, string comment, CancellationToken cancellationToken)
    {
        if (!int.TryParse(ticketId.Value, out var id))
            return;

        var client = CreateClient();
        var patch = new JsonPatchDocument
        {
            new()
            {
                Operation = Operation.Add,
                Path = "/fields/System.History",
                Value = comment
            }
        };
        await client.UpdateWorkItemAsync(patch, project, id, cancellationToken: cancellationToken);
    }

    public async Task CloseTicketAsync(
        TicketId ticketId, string resolution, CancellationToken cancellationToken)
    {
        if (!int.TryParse(ticketId.Value, out var id))
            return;

        var client = CreateClient();
        var patch = new JsonPatchDocument
        {
            new()
            {
                Operation = Operation.Add,
                Path = "/fields/System.History",
                Value = resolution
            },
            new()
            {
                Operation = Operation.Add,
                Path = "/fields/System.State",
                Value = _doneStatus
            }
        };
        await client.UpdateWorkItemAsync(patch, project, id, cancellationToken: cancellationToken);
    }

    public async Task TransitionToAsync(
        TicketId ticketId, string statusName, CancellationToken cancellationToken)
    {
        if (!int.TryParse(ticketId.Value, out var id))
            return;

        var client = CreateClient();
        var patch = new JsonPatchDocument
        {
            new()
            {
                Operation = Operation.Add,
                Path = "/fields/System.State",
                Value = statusName
            }
        };
        await client.UpdateWorkItemAsync(patch, project, id, cancellationToken: cancellationToken);
    }

    private static string GetField(IDictionary<string, object> fields, string key)
    {
        return fields.TryGetValue(key, out var value) ? value?.ToString() ?? "" : "";
    }

    private static string? GetFieldOrNull(IDictionary<string, object> fields, string key)
    {
        return fields.TryGetValue(key, out var value) ? value?.ToString() : null;
    }
}
