using System.Diagnostics;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Markdig;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Thin Azure DevOps WorkItemTracking orchestrator. Field mapping in <see cref="AzureDevOpsFieldMapper"/>;
/// cached VssConnection in <see cref="AzureDevOpsConnectionCache"/>; WIQL in <see cref="AzureDevOpsWorkItemLister"/>.
/// </summary>
public sealed class AzureDevOpsTicketProvider : ITicketProvider
{
    private readonly string _project;
    private readonly string _organizationUrl;
    private readonly string _doneStatus;
    private readonly AzureDevOpsAttachmentLoader _attachmentLoader;
    private readonly AzureDevOpsFieldMapper _mapper;
    private readonly AzureDevOpsCommentMapper _commentMapper = new();
    private readonly AzureDevOpsConnectionCache _connections;
    private readonly AzureDevOpsWorkItemLister _lister;
    private readonly ILogger _logger;
    private readonly TrackerParentLink _parentLink;
    private readonly AzureDevOpsTicketFinalizer _finalizer;
    private readonly AzureDevOpsTicketCreator _creator;

    public string ProviderType => "AzureDevOps";

    public AzureDevOpsTicketProvider(
        AzureDevOpsTicketConnection connection,
        AzureDevOpsAttachmentLoader attachmentLoader,
        AzureDevOpsFieldMapper mapper,
        ILogger<AzureDevOpsTicketProvider> logger,
        IReadOnlyList<string>? openStates = null,
        string? doneStatus = null,
        IReadOnlyList<string>? extraFields = null)
    {
        _project = connection.Project;
        _organizationUrl = connection.OrganizationUrl;
        _doneStatus = doneStatus ?? "Closed";
        _attachmentLoader = attachmentLoader;
        _mapper = mapper;
        _connections = new AzureDevOpsConnectionCache(connection, logger);
        _lister = new AzureDevOpsWorkItemLister(_connections, mapper, connection.Project, openStates, extraFields, logger);
        _logger = logger;
        _parentLink = new TrackerParentLink("Azure DevOps", logger);
        _finalizer = new AzureDevOpsTicketFinalizer(_doneStatus, WriteFinalizeAsync, logger);
        _creator = new AzureDevOpsTicketCreator(
            connection.OrganizationUrl, connection.Project,
            (patch, type, ct) => _connections.CreateClient()
                .CreateWorkItemAsync(patch, connection.Project, type, cancellationToken: ct),
            logger);
    }

    public async Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var wiql = new Wiql { Query = "SELECT [System.Id] FROM WorkItems WHERE [System.Id] = 0" };
            await _connections.CreateClient()
                .QueryByWiqlAsync(wiql, _project, cancellationToken: cancellationToken);
            return ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Azure DevOps tracker probe failed for {Project}", _project);
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    public async Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(ticketId.Value, out var id)) throw new TicketNotFoundException(ticketId);
        _logger.LogDebug("AzDO GetTicket #{Ticket}: GetWorkItemAsync project={Project} id={Id}",
            ticketId.Value, _project, id);
        var workItem = await _connections.CreateClient()
            .GetWorkItemAsync(_project, id, cancellationToken: cancellationToken)
            ?? throw new TicketNotFoundException(ticketId);
        var ticket = _mapper.Map(ticketId, workItem.Fields);
        _logger.LogDebug("AzDO GetTicket #{Ticket}: status={Status} labels={Count}",
            ticketId.Value, ticket.Status, ticket.Labels?.Count ?? 0);
        return ticket;
    }

    public Task<IReadOnlyList<Ticket>> ListOpenAsync(CancellationToken cancellationToken) =>
        _lister.ListAsync(extraWhere: null, "open", cancellationToken);

    // The lister's WIQL carries the per-project status/tag/area-path branches, so only candidates return.
    public Task<IReadOnlyList<Ticket>> ListClaimableAsync(
        DiscoveryQuery query, CancellationToken cancellationToken) =>
        _lister.ListClaimableAsync(query, cancellationToken);

    public Task<IReadOnlyList<Ticket>> ListByLifecycleStatusAsync(
        TicketLifecycleStatus status, CancellationToken cancellationToken) =>
        _lister.ListAsync(
            $"[System.Tags] CONTAINS '{LifecycleLabels.For(status)}'", $"lifecycle={status}", cancellationToken);

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

    // System.Description renders HTML, so the markdown is converted before it travels.
    public Task<CreatedTicket> CreateAsync(
        string title, string description, IReadOnlyList<string> labels, string? kind,
        CancellationToken cancellationToken) =>
        _creator.CreateAsync(title, ToHtml(description), labels, kind, cancellationToken);

    // System.Tags is ONE semicolon-joined scalar and Azure DevOps has no append op for it, so the
    // field is read and rewritten whole. A tag already on the work item is not written again — the
    // rewrite would be a no-op PATCH that still bumps System.Rev for every concurrent observer.
    public async Task<bool> AddLabelAsync(TicketId ticketId, string label, CancellationToken ct)
    {
        var patch = BuildAddTagPatch((await GetTicketAsync(ticketId, ct)).Labels, label);
        if (patch is not null) await PatchAsync(ticketId, patch, ct);
        return true;
    }

    /// <summary>Null when the tag is already there: rewriting it would be a no-op PATCH that still
    /// bumps System.Rev and fails the next write of every concurrent observer with TF26071.</summary>
    internal static JsonPatchDocument? BuildAddTagPatch(IReadOnlyList<string> tags, string label) =>
        tags.Contains(label, StringComparer.OrdinalIgnoreCase)
            ? null
            : [Op("/fields/System.Tags", string.Join("; ", tags.Append(label)))];

    public async Task<ParentLinkResult> LinkToParentAsync(
        CreatedTicket child, TicketId parent, CancellationToken cancellationToken) =>
        int.TryParse(parent.Value, out var parentId) && int.TryParse(child.Id.Value, out _)
            ? await _parentLink.AttemptAsync(() =>
                PatchAsync(child.Id, BuildParentLinkPatch(_organizationUrl, parentId), cancellationToken), cancellationToken)
            : ParentLinkResult.Failed($"'{child.Id.Value}' or '{parent.Value}' is not an Azure DevOps work item id.");

    // A relation names its target by the REST url, not the web url a person follows.
    internal static JsonPatchDocument BuildParentLinkPatch(string organizationUrl, int parentId) =>
        [Op("/relations/-", new WorkItemRelation
            { Rel = "System.LinkTypes.Hierarchy-Reverse", Url = $"{organizationUrl.TrimEnd('/')}/_apis/wit/workItems/{parentId}" })];

    public async Task<IReadOnlyList<TicketDocumentAttachment>> DownloadDocumentAttachmentsAsync(
        TicketId ticketId, CancellationToken cancellationToken) =>
        await TicketDocumentAttachmentDownloader.DownloadAllAsync(
            await GetAttachmentRefsAsync(ticketId, cancellationToken),
            _attachmentLoader.DownloadAsync, cancellationToken);

    // The Comments resource is the store the System.History PATCHes land in. Transport
    // failures propagate — FetchTicketHandler owns fail-soft.
    public async Task<IReadOnlyList<TicketComment>> GetCommentsAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(ticketId.Value, out var id)) return [];
        var comments = await _connections.CreateClient()
            .GetCommentsAsync(_project, id, cancellationToken: cancellationToken);
        return _commentMapper.MapMany(comments?.Comments);
    }

    public Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken)
        => PatchAsync(ticketId, [Op("/fields/System.History", ToHtml(comment))], cancellationToken);

    public Task CloseTicketAsync(TicketId ticketId, string resolution, CancellationToken cancellationToken)
        => WriteFinalizeAsync(ticketId, resolution, _doneStatus, cancellationToken);

    public async Task<bool> TransitionToAsync(TicketId ticketId, string statusName, CancellationToken cancellationToken)
    {
        await PatchAsync(ticketId, [Op("/fields/System.State", statusName)], cancellationToken);
        return true; // AzDO refuses a state it cannot write by throwing, so a PATCH that returned landed.
    }

    // One PATCH: AzDO bumps System.Rev on every write, so a comment and a transition sent
    // apart race any concurrent observer and the second fails with TF26071.
    public Task<TicketFinalizeResult> FinalizeAsync(
        TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken)
        => _finalizer.FinalizeAsync(ticketId, comment, doneStatus, cancellationToken);

    private Task WriteFinalizeAsync(TicketId ticketId, string comment, string state, CancellationToken ct)
        => PatchAsync(ticketId, [Op("/fields/System.History", ToHtml(comment)), Op("/fields/System.State", state)], ct);

    private async Task PatchAsync(TicketId ticketId, JsonPatchDocument patch, CancellationToken cancellationToken)
    {
        if (!int.TryParse(ticketId.Value, out var id)) throw new TicketNotFoundException(ticketId);
        // Writes that bypass the lifecycle transitioner are logged through the same lens.
        _logger.LogInformation(
            "TICKET WRITE #{Ticket}: fields[{Paths}] <- {Caller}",
            ticketId.Value, string.Join(", ", patch.Select(p => p.Path)), TicketWriteAudit.Caller());
        await _connections.CreateClient().UpdateWorkItemAsync(patch, _project, id, cancellationToken: cancellationToken);
    }

    private static JsonPatchOperation Op(string path, object value) =>
        new() { Operation = Operation.Add, Path = path, Value = value };

    // System.History renders HTML, and raw markdown loses its line breaks; the SDK's
    // CommentCreate cannot send format=markdown, so the conversion happens here.
    private static readonly MarkdownPipeline MarkdownPipeline =
        new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    private static string ToHtml(string markdown) =>
        string.IsNullOrEmpty(markdown) ? markdown : Markdown.ToHtml(markdown, MarkdownPipeline);
}
