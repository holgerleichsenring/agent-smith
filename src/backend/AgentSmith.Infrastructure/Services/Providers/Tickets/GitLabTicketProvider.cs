using System.Diagnostics;
using System.Net.Http;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>Thin GitLab REST v4 orchestrator; mapping, listing, auth and attachments live in their own types.</summary>
public sealed class GitLabTicketProvider : ITicketProvider
{
    private readonly string _baseUrl;
    private readonly string _projectPath;
    private readonly string _privateToken;
    private readonly HttpClient _httpClient;
    private readonly TicketProviderHttpClient _http;
    private readonly GitLabAttachmentLoader _attachmentLoader;
    private readonly GitLabFieldMapper _mapper;
    private readonly GitLabCommentMapper _commentMapper = new();
    private readonly GitLabIssueLister _lister;
    private readonly ILogger _logger;
    private readonly TrackerParentLink _parentLink;
    private readonly GitLabTicketFinalizer _finalizer;

    public string ProviderType => "GitLab";

    public GitLabTicketProvider(
        GitLabTicketConnection connection,
        HttpClient httpClient, GitLabAttachmentLoader attachmentLoader,
        GitLabFieldMapper mapper, ILogger<GitLabTicketProvider> logger)
    {
        _baseUrl = connection.BaseUrl.TrimEnd('/');
        _projectPath = connection.ProjectPath;
        _privateToken = connection.PrivateToken;
        _httpClient = httpClient;
        _http = TicketProviderHttpClient.WithPrivateToken(httpClient, connection.PrivateToken);
        _attachmentLoader = attachmentLoader;
        _mapper = mapper;
        _lister = new GitLabIssueLister(_http, mapper, connection, logger);
        _logger = logger;
        _parentLink = new TrackerParentLink("GitLab", logger);
        _finalizer = new GitLabTicketFinalizer(UpdateStatusAsync, CloseTicketAsync, TransitionToAsync);
    }

    public async Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var _ = await _http.SendForJsonOrThrowAsync(
                HttpMethod.Get, $"{_baseUrl}/api/v4/projects/{_projectPath}", null, cancellationToken);
            return ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GitLab tracker probe failed for {Project}", _projectPath);
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    public async Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        var url = IssueUrl(ticketId);
        _logger.LogDebug("GitLab GetTicket #{Ticket}: GET {Url}", ticketId.Value, url);
        var doc = await _http.SendForJsonAsync(HttpMethod.Get, url, null, cancellationToken)
            ?? throw new TicketNotFoundException(ticketId);
        using (doc)
        {
            var ticket = _mapper.Map(ticketId, doc.RootElement);
            _logger.LogDebug("GitLab GetTicket #{Ticket}: status={Status} labels={Count}",
                ticketId.Value, ticket.Status, ticket.Labels?.Count ?? 0);
            return ticket;
        }
    }

    public Task<IReadOnlyList<Ticket>> ListOpenAsync(CancellationToken cancellationToken)
        => _lister.ListOpenAsync(cancellationToken);

    // Issues are opened/closed only: narrow by labels when every branch is tag-based, else stay broad.
    // The trigger-label guard stays in-process — GitLab's ?labels= is AND-only with no prefix match.
    public Task<IReadOnlyList<Ticket>> ListClaimableAsync(
        DiscoveryQuery query, CancellationToken cancellationToken)
        => query.AllTagLabelsOrNull() is { Count: > 0 } labels
            ? _lister.SearchAsync(labels, "claimable", cancellationToken)
            : ListOpenAsync(cancellationToken);

    public Task<IReadOnlyList<Ticket>> ListByLifecycleStatusAsync(
        TicketLifecycleStatus status, CancellationToken cancellationToken)
        => _lister.SearchAsync([LifecycleLabels.For(status)], $"lifecycle={status}", cancellationToken);

    public async Task<IReadOnlyList<AttachmentRef>> GetAttachmentRefsAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        try
        {
            var ticket = await GetTicketAsync(ticketId, cancellationToken);
            var loader = new GitLabAttachmentLoader(
                new GitLabTicketConnection(_baseUrl, _projectPath, _privateToken),
                _httpClient, NullLogger.Instance);
            return loader.ParseRefs(ticket.Description);
        }
        catch { return []; }
    }

    public async Task<IReadOnlyList<TicketImageAttachment>> DownloadImageAttachmentsAsync(TicketId ticketId, CancellationToken cancellationToken) =>
        await TicketImageAttachmentDownloader.DownloadAllAsync(
            await GetAttachmentRefsAsync(ticketId, cancellationToken),
            _attachmentLoader.DownloadAsync, cancellationToken);

    // GitLab has no work-item kind: an issue accepts exactly two state events, close and
    // reopen, so the kind the port carries never reaches its payload.
    public async Task<CreatedTicket> CreateAsync(
        string title, string description, IReadOnlyList<string> labels, string? kind, CancellationToken cancellationToken)
    {
        object body = labels.Count > 0
            ? new { title, description, labels = string.Join(",", labels) }
            : new { title, description };
        using var doc = await _http.SendForJsonOrThrowAsync(HttpMethod.Post,
            $"{_baseUrl}/api/v4/projects/{_projectPath}/issues", body, cancellationToken);
        var iid = doc.RootElement.GetProperty("iid").GetInt32();
        var webUrl = doc.RootElement.TryGetProperty("web_url", out var url) ? url.GetString() : null;
        _logger.LogInformation("GitLab created issue #{Iid} in {Project}", iid, _projectPath);
        return new CreatedTicket(new TicketId(iid.ToString()), webUrl);
    }

    // add_labels appends without reading: the value is sent WHOLE, and a comma in it would split
    // into two labels — which is why a value carrying one never reaches this call.
    public async Task<bool> AddLabelAsync(TicketId ticketId, string label, CancellationToken ct)
    {
        await _http.SendAsync(HttpMethod.Put, IssueUrl(ticketId), new { add_labels = label }, ct);
        return true;
    }

    // relates_to is the one issue link the free tier offers; the body takes the decoded project path.
    public Task<ParentLinkResult> LinkToParentAsync(
        CreatedTicket child, TicketId parent, CancellationToken cancellationToken) =>
        _parentLink.AttemptAsync(() => _http.SendAsync(HttpMethod.Post, $"{IssueUrl(child.Id)}/links",
            new { target_project_id = Uri.UnescapeDataString(_projectPath), target_issue_iid = parent.Value, link_type = "relates_to" },
            cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<TicketDocumentAttachment>> DownloadDocumentAttachmentsAsync(TicketId ticketId, CancellationToken cancellationToken) =>
        await TicketDocumentAttachmentDownloader.DownloadAllAsync(
            await GetAttachmentRefsAsync(ticketId, cancellationToken),
            _attachmentLoader.DownloadAsync, cancellationToken);

    // p0317: the ticket conversation — the same notes endpoint UpdateStatusAsync
    // posts to. Transport failures propagate — FetchTicketHandler owns fail-soft.
    public async Task<IReadOnlyList<TicketComment>> GetCommentsAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        using var doc = await _http.SendForJsonOrThrowAsync(
            HttpMethod.Get, $"{IssueUrl(ticketId)}/notes?per_page=100", null, cancellationToken);
        return _commentMapper.MapMany(doc.RootElement);
    }

    public Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken) =>
        _http.SendAsync(HttpMethod.Post, $"{IssueUrl(ticketId)}/notes",
            new { body = comment }, cancellationToken);

    public async Task CloseTicketAsync(TicketId ticketId, string resolution, CancellationToken cancellationToken)
    {
        await UpdateStatusAsync(ticketId, resolution, cancellationToken);
        await TransitionToAsync(ticketId, "closed", cancellationToken);
    }

    public async Task<bool> TransitionToAsync(TicketId ticketId, string statusName, CancellationToken cancellationToken)
    {
        await _http.SendAsync(HttpMethod.Put, IssueUrl(ticketId), new { state_event = ToStateEvent(statusName) }, cancellationToken);
        return true; // GitLab fails the request when it refuses the state_event, so a PUT that returned landed.
    }

    // GitLab issues have no rev-guard; sequential note + state change is safe.
    public Task<TicketFinalizeResult> FinalizeAsync(
        TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken)
        => _finalizer.FinalizeAsync(ticketId, comment, doneStatus, cancellationToken);

    private string IssueUrl(TicketId ticketId) =>
        $"{_baseUrl}/api/v4/projects/{_projectPath}/issues/{ticketId.Value}";

    private static string ToStateEvent(string statusName) => statusName.ToLowerInvariant() switch
    {
        "closed" => "close",
        "opened" or "open" or "reopen" => "reopen",
        var s => s
    };
}
