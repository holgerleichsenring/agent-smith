using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-14-dbcem: a Dependency link on Azure DevOps — the dependent gains a Dependency-Reverse relation, which names
/// its target as the PREDECESSOR. A relation names its target by the REST url, not the web url a person follows.
/// </summary>
public sealed class AzureDevOpsRequiresLink : ITicketRequiresLink
{
    internal const string Relation = "System.LinkTypes.Dependency-Reverse";

    private readonly string _organizationUrl;
    private readonly Func<JsonPatchDocument, int, CancellationToken, Task> _patch;
    private readonly ILogger _logger;

    public AzureDevOpsRequiresLink(AzureDevOpsTicketConnection connection, ILogger logger)
        : this(connection.OrganizationUrl, Sender(new AzureDevOpsConnectionCache(connection, logger), connection.Project), logger)
    {
    }

    internal AzureDevOpsRequiresLink(string organizationUrl, Func<JsonPatchDocument, int, CancellationToken, Task> patch, ILogger logger)
    {
        (_organizationUrl, _patch, _logger) = (organizationUrl, patch, logger);
    }

    public async Task<RequiresLinkResult> LinkAsync(TicketId dependent, TicketId required, CancellationToken cancellationToken)
    {
        if (!int.TryParse(dependent.Value, out var dependentId) || !int.TryParse(required.Value, out var requiredId))
            return RequiresLinkResult.Refused("not an Azure DevOps work item id");
        try
        {
            await _patch(Patch(_organizationUrl, requiredId), dependentId, cancellationToken);
            return RequiresLinkResult.Done;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Work item {Dependent} could not be linked to the work item {Required} it requires", dependentId, requiredId);
            return RequiresLinkRefusal.Of(ex);
        }
    }

    internal static JsonPatchDocument Patch(string organizationUrl, int requiredId) =>
    [
        new JsonPatchOperation
        {
            Operation = Operation.Add, Path = "/relations/-",
            Value = new WorkItemRelation { Rel = Relation, Url = $"{organizationUrl.TrimEnd('/')}/_apis/wit/workItems/{requiredId}" },
        },
    ];

    private static Func<JsonPatchDocument, int, CancellationToken, Task> Sender(AzureDevOpsConnectionCache connections, string project) =>
        (patch, id, ct) => connections.CreateClient().UpdateWorkItemAsync(patch, project, id, cancellationToken: ct);
}
