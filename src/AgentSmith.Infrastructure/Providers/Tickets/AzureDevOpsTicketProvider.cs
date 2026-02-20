using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.ValueObjects;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Wiql = Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.Wiql;

namespace AgentSmith.Infrastructure.Providers.Tickets;

/// <summary>
/// Fetches work items from Azure DevOps.
/// </summary>
public sealed class AzureDevOpsTicketProvider(
    string organizationUrl,
    string project,
    string personalAccessToken) : ITicketProvider
{
    public string ProviderType => "AzureDevOps";

    public async Task<Ticket> GetTicketAsync(
        TicketId ticketId, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        var workItem = await FetchWorkItem(client, ticketId, cancellationToken);
        return MapToTicket(ticketId, workItem.Fields);
    }

    public async Task<IReadOnlyList<Ticket>> ListOpenAsync(
        CancellationToken cancellationToken = default)
    {
        var client = CreateClient();

        var wiql = new Wiql
        {
            Query = $"""
                SELECT [System.Id]
                FROM WorkItems
                WHERE [System.TeamProject] = '{project}'
                  AND [System.State] <> 'Closed'
                  AND [System.State] <> 'Resolved'
                  AND [System.State] <> 'Done'
                ORDER BY [System.ChangedDate] DESC
                """
        };

        var result = await client.QueryByWiqlAsync(wiql, project, top: 50,
            cancellationToken: cancellationToken);

        if (result.WorkItems is null || !result.WorkItems.Any())
            return Array.Empty<Ticket>();

        var ids = result.WorkItems.Select(w => w.Id).ToArray();

        var workItems = await client.GetWorkItemsAsync(
            ids,
            fields: ["System.Id", "System.Title", "System.Description",
                     "System.State", "Microsoft.VSTS.Common.AcceptanceCriteria"],
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

    private WorkItemTrackingHttpClient CreateClient()
    {
        var credentials = new VssBasicCredential(string.Empty, personalAccessToken);
        var connection = new VssConnection(new Uri(organizationUrl), credentials);
        return connection.GetClient<WorkItemTrackingHttpClient>();
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
        return new Ticket(
            ticketId,
            GetField(fields, "System.Title"),
            GetField(fields, "System.Description"),
            GetFieldOrNull(fields, "Microsoft.VSTS.Common.AcceptanceCriteria"),
            GetField(fields, "System.State"),
            "AzureDevOps");
    }

    public async Task UpdateStatusAsync(
        TicketId ticketId, string comment, CancellationToken cancellationToken = default)
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
        TicketId ticketId, string resolution, CancellationToken cancellationToken = default)
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
                Value = "Closed"
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
