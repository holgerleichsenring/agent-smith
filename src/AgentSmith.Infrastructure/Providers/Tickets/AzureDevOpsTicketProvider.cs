using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.ValueObjects;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

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

    private static string GetField(IDictionary<string, object> fields, string key)
    {
        return fields.TryGetValue(key, out var value) ? value?.ToString() ?? "" : "";
    }

    private static string? GetFieldOrNull(IDictionary<string, object> fields, string key)
    {
        return fields.TryGetValue(key, out var value) ? value?.ToString() : null;
    }
}
