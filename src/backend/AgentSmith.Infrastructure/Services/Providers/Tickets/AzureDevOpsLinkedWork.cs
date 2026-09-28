using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-28-1da5c: the branches and pull requests Azure DevOps links to a work item.
/// <para>
/// This is the only one of the four trackers that links BOTH, and it does so as work-item
/// RELATIONS — which the single fetch does not ask for, because nothing needed them until now.
/// Their addresses are vstfs URIs rather than URLs, so reading one back is a parse; this repository
/// is on record disliking exactly that ("recovering an id from a web url is a parser per
/// provider"), and the cost is accepted here for two shapes that Azure DevOps defines rather than
/// infers.
/// </para>
/// </summary>
public sealed class AzureDevOpsLinkedWork : ITicketLinkedWork
{
    private readonly AzureDevOpsConnectionCache _connections;
    private readonly string _project;
    private readonly ILogger _logger;

    public AzureDevOpsLinkedWork(AzureDevOpsTicketConnection connection, ILogger logger)
    {
        _connections = new AzureDevOpsConnectionCache(connection, logger);
        _project = connection.Project;
        _logger = logger;
    }

    public async Task<TicketLinkedWorkResult> ForAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticketId);
        if (!int.TryParse(ticketId.Value, out var id))
            return TicketLinkedWorkResult.Refused($"'{ticketId.Value}' is not a work item id.");
        try
        {
            var item = await _connections.CreateClient().GetWorkItemAsync(
                _project, id, expand: WorkItemExpand.Relations, cancellationToken: cancellationToken);
            return TicketLinkedWorkResult.Of(
                (item?.Relations ?? []).Select(Linked).OfType<TicketLinkedWork>());
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex,
                "Azure DevOps could not be asked what it links to work item {Ticket}", ticketId.Value);
            return TicketLinkedWorkResult.Refused(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    /// <summary>
    /// A relation is linked work only when its URL is one of the two artifact shapes Azure DevOps
    /// defines for it. Everything else on a work item — parents, children, attachments, other work
    /// — is a relation too and is not what was asked.
    /// </summary>
    internal static TicketLinkedWork? Linked(WorkItemRelation relation)
    {
        var url = relation?.Url ?? string.Empty;
        if (url.StartsWith("vstfs:///Git/PullRequestId/", StringComparison.OrdinalIgnoreCase))
            return new TicketLinkedWork("pull request", $"!{Artifact(url)}", Comment(relation!), null);
        if (url.StartsWith("vstfs:///Git/Ref/", StringComparison.OrdinalIgnoreCase))
            return new TicketLinkedWork("branch", Branch(Artifact(url)), null, null);
        return null;
    }

    /// <summary>
    /// A vstfs artifact address is project%2Frepo%2Fwhat-was-linked — and the last part is not the
    /// last SEGMENT: a branch name carries its own encoded slashes, so everything after the second
    /// separator belongs to it.
    /// </summary>
    private static string Artifact(string url)
    {
        var tail = url[(url.LastIndexOf('/') + 1)..].Split("%2F", StringSplitOptions.None);
        return tail.Length <= 2 ? tail[^1] : string.Join("/", tail.Skip(2));
    }

    /// <summary>Azure DevOps prefixes a linked branch ref with GB.</summary>
    private static string Branch(string artifact) =>
        artifact.StartsWith("GB", StringComparison.Ordinal) ? artifact[2..] : artifact;

    private static string? Comment(WorkItemRelation relation) =>
        relation.Attributes is not null && relation.Attributes.TryGetValue("name", out var name)
            ? name?.ToString()
            : null;
}
