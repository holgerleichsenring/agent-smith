using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Owns the Azure DevOps create: the field patch, the WORK-ITEM TYPE it is posted under, and
/// the web url of what came back. The type is what the 2026-09-18-b4f0 incident was about —
/// a filing created a Task, the project's configured failed_status was a state a Task does
/// not have, and every run on that ticket ended with a rejected status write.
/// <para>
/// The SDK call arrives as a DELEGATE — the shape <see cref="AzureDevOpsTicketFinalizer"/>
/// already takes its write in — because the client the provider builds in its own constructor
/// returns a concrete type no test can substitute. Extracting the create without the delegate
/// would carry the unseamed call along with it.
/// </para>
/// </summary>
public sealed class AzureDevOpsTicketCreator(
    string organizationUrl,
    string project,
    Func<JsonPatchDocument, string, CancellationToken, Task<WorkItem>> create,
    ILogger logger)
{
    /// <summary>
    /// What this create posted under before an operator could choose, and what exists in every
    /// process template. Applied in the ONE clause that reads the configured kind, so an
    /// installation that configures nothing creates exactly what it created before.
    /// </summary>
    private const string DefaultWorkItemType = "Task";

    public async Task<CreatedTicket> CreateAsync(
        string title, string descriptionHtml, IReadOnlyList<string> labels, string? kind,
        CancellationToken cancellationToken)
    {
        var patch = BuildCreatePatch(title, descriptionHtml, labels);
        var type = string.IsNullOrWhiteSpace(kind) ? DefaultWorkItemType : kind;
        logger.LogInformation(
            "TICKET WRITE create ({Project}): type={Type} fields[{Paths}] <- {Caller}",
            project, type, string.Join(", ", patch.Select(p => p.Path)), TicketWriteAudit.Caller());
        var workItem = await create(patch, type, cancellationToken);
        var id = workItem.Id
            ?? throw new InvalidOperationException("Azure DevOps returned a created work item without an id.");
        return new CreatedTicket(new TicketId(id.ToString()), WorkItemWebUrl(organizationUrl, project, id));
    }

    internal static JsonPatchDocument BuildCreatePatch(
        string title, string descriptionHtml, IReadOnlyList<string> labels)
    {
        var patch = new JsonPatchDocument { Op("/fields/System.Title", title) };
        if (!string.IsNullOrEmpty(descriptionHtml))
            patch.Add(Op("/fields/System.Description", descriptionHtml));
        if (labels.Count > 0)
            patch.Add(Op("/fields/System.Tags", string.Join("; ", labels)));
        return patch;
    }

    internal static string WorkItemWebUrl(string organizationUrl, string project, int id) =>
        $"{organizationUrl.TrimEnd('/')}/{Uri.EscapeDataString(project)}/_workitems/edit/{id}";

    private static JsonPatchOperation Op(string path, object value) =>
        new() { Operation = Operation.Add, Path = path, Value = value };
}
