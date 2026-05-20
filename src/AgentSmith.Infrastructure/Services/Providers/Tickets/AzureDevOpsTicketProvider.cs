using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Markdig;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: thin Azure DevOps WorkItemTracking orchestrator. Field mapping
/// in <see cref="AzureDevOpsFieldMapper"/>; cached VssConnection in
/// <see cref="AzureDevOpsConnectionCache"/>; WIQL queries +
/// transport-failure recovery in <see cref="AzureDevOpsWorkItemLister"/>.
/// </summary>
public sealed class AzureDevOpsTicketProvider : ITicketProvider
{
    private readonly string _project;
    private readonly string _doneStatus;
    private readonly AzureDevOpsAttachmentLoader _attachmentLoader;
    private readonly AzureDevOpsFieldMapper _mapper;
    private readonly AzureDevOpsConnectionCache _connections;
    private readonly AzureDevOpsWorkItemLister _lister;

    public string ProviderType => "AzureDevOps";

    public AzureDevOpsTicketProvider(
        string organizationUrl, string project, string personalAccessToken,
        AzureDevOpsAttachmentLoader attachmentLoader,
        AzureDevOpsFieldMapper mapper,
        ILogger<AzureDevOpsTicketProvider> logger,
        IReadOnlyList<string>? openStates = null,
        string? doneStatus = null,
        IReadOnlyList<string>? extraFields = null)
    {
        _project = project;
        _doneStatus = doneStatus ?? "Closed";
        _attachmentLoader = attachmentLoader;
        _mapper = mapper;
        _connections = new AzureDevOpsConnectionCache(organizationUrl, personalAccessToken, logger);
        _lister = new AzureDevOpsWorkItemLister(_connections, mapper, project, openStates, extraFields, logger);
    }

    public async Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(ticketId.Value, out var id)) throw new TicketNotFoundException(ticketId);
        var workItem = await _connections.CreateClient()
            .GetWorkItemAsync(_project, id, cancellationToken: cancellationToken)
            ?? throw new TicketNotFoundException(ticketId);
        return _mapper.Map(ticketId, workItem.Fields);
    }

    public Task<IReadOnlyList<Ticket>> ListOpenAsync(CancellationToken cancellationToken) =>
        _lister.ListAsync(extraWhere: null, "open", cancellationToken);

    public Task<IReadOnlyList<Ticket>> ListByLifecycleStatusAsync(
        TicketLifecycleStatus status, CancellationToken cancellationToken) =>
        _lister.ListAsync(
            $"[System.Tags] CONTAINS '{LifecycleLabels.For(status)}'",
            $"lifecycle={status}", cancellationToken);

    public Task<IReadOnlyList<Ticket>> ListByLabelsInOpenStatesAsync(
        IReadOnlyCollection<string> labels, CancellationToken cancellationToken)
    {
        if (labels.Count == 0) return Task.FromResult<IReadOnlyList<Ticket>>([]);
        var tagOr = string.Join(" OR ", labels.Select(l => $"[System.Tags] CONTAINS '{EscapeWiql(l)}'"));
        return _lister.ListAsync($"({tagOr})", $"labels=[{string.Join(", ", labels)}]", cancellationToken);
    }

    public Task<IReadOnlyList<AttachmentRef>> GetAttachmentRefsAsync(
        TicketId ticketId, CancellationToken cancellationToken)
        => int.TryParse(ticketId.Value, out var id)
            ? TryGetAttachmentRefs(id, cancellationToken)
            : Task.FromResult<IReadOnlyList<AttachmentRef>>([]);

    private async Task<IReadOnlyList<AttachmentRef>> TryGetAttachmentRefs(int id, CancellationToken ct)
    {
        try { return await _attachmentLoader.GetRefsAsync(id, ct); } catch { return []; }
    }

    public async Task<IReadOnlyList<TicketImageAttachment>> DownloadImageAttachmentsAsync(
        TicketId ticketId, CancellationToken cancellationToken) =>
        await TicketImageAttachmentDownloader.DownloadAllAsync(
            await GetAttachmentRefsAsync(ticketId, cancellationToken),
            _attachmentLoader.DownloadAsync, cancellationToken);

    public Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken)
        => PatchAsync(ticketId, [Op("/fields/System.History", ToHtml(comment))], cancellationToken);

    public Task CloseTicketAsync(TicketId ticketId, string resolution, CancellationToken cancellationToken)
        => PatchAsync(ticketId,
            [Op("/fields/System.History", ToHtml(resolution)), Op("/fields/System.State", _doneStatus)],
            cancellationToken);

    public Task TransitionToAsync(TicketId ticketId, string statusName, CancellationToken cancellationToken)
        => PatchAsync(ticketId, [Op("/fields/System.State", statusName)], cancellationToken);

    private async Task PatchAsync(TicketId ticketId, JsonPatchDocument patch, CancellationToken cancellationToken)
    {
        if (!int.TryParse(ticketId.Value, out var id)) return;
        await _connections.CreateClient().UpdateWorkItemAsync(patch, _project, id, cancellationToken: cancellationToken);
    }

    private static JsonPatchOperation Op(string path, string value) =>
        new() { Operation = Operation.Add, Path = path, Value = value };

    // ADO's System.History field renders HTML natively; sending raw markdown
    // produces plain-text-with-no-line-breaks in the UI. The dedicated Comments
    // REST API accepts a `format=markdown` parameter but the bundled SDK does
    // not surface it (CommentCreate exposes Text only). Converting client-side
    // keeps the existing UpdateWorkItemAsync call path intact.
    private static readonly MarkdownPipeline MarkdownPipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    private static string ToHtml(string markdown) =>
        string.IsNullOrEmpty(markdown) ? markdown : Markdown.ToHtml(markdown, MarkdownPipeline);

    private static string EscapeWiql(string s) => s.Replace("'", "''");
}
