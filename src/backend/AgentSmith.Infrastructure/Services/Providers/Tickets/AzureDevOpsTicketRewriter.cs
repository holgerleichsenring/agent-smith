using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using Markdig;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-25-8e51e: rewrites the framework's region of an Azure DevOps work item's body.
/// <para>
/// Azure DevOps converts the markdown a create sends into HTML and hands the HTML back, so the
/// region is found and replaced IN HTML and the replacement is converted the same way the create
/// converts — the marker pair is an HTML comment and survives both directions. Everything outside
/// the markers is left exactly as the field holds it.
/// </para>
/// <para>
/// The field written is the field the work item's own TYPE reads
/// (<see cref="AzureDevOpsBodyField"/>): a Bug keeps its body in reproduction steps, and writing
/// Description there would leave the ticket with a second body no reader ever sees.
/// </para>
/// </summary>
public sealed class AzureDevOpsTicketRewriter : ITicketRewriter
{
    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    private readonly string _project;
    private readonly AzureDevOpsConnectionCache _connections;
    private readonly ILogger _logger;

    public AzureDevOpsTicketRewriter(AzureDevOpsTicketConnection connection, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(connection);
        _project = connection.Project;
        _connections = new AzureDevOpsConnectionCache(connection, logger);
        _logger = logger;
    }

    public async Task<TicketRewriteResult> RewriteRegionAsync(
        TicketId ticketId, string region, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticketId);
        if (!int.TryParse(ticketId.Value, out var id))
            return TicketRewriteResult.Failed($"'{ticketId.Value}' is not an Azure DevOps work item id.");
        try
        {
            var client = _connections.CreateClient();
            var workItem = await client.GetWorkItemAsync(_project, id, cancellationToken: cancellationToken);
            if (workItem?.Fields is not { } fields)
                return TicketRewriteResult.Failed($"Azure DevOps has no work item {id}.");
            var field = AzureDevOpsBodyField.Of(fields);
            if (FramedTicketRegion.Replace(AzureDevOpsBodyField.Read(fields, field), ToHtml(region))
                is not { } rewritten)
                return TicketRewriteResult.Unsupported(TicketRegionRefusal.NoRegion);

            _logger.LogInformation(
                "TICKET WRITE #{Ticket}: fields[/fields/{Field}] region <- {Caller}",
                ticketId.Value, field, TicketWriteAudit.Caller());
            await client.UpdateWorkItemAsync(
                Patch(field, rewritten), _project, id, cancellationToken: cancellationToken);
            return TicketRewriteResult.Ok;
        }
        // The conversation that asked for the amendment is told; a thrown turn tells it nothing.
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Rewriting the region of work item {Ticket} failed", ticketId.Value);
            return TicketRewriteResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    private static JsonPatchDocument Patch(string field, string html) =>
        [new JsonPatchOperation { Operation = Operation.Add, Path = $"/fields/{field}", Value = html }];

    // The same conversion the create does, so an amended region renders like the filed one.
    private static string ToHtml(string markdown) =>
        string.IsNullOrEmpty(markdown) ? markdown : Markdown.ToHtml(markdown, Pipeline);
}
