using System.Diagnostics;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Thin Jira Cloud REST v3 orchestrator. Mapping in <see cref="JiraFieldMapper"/>, ADF in
/// <see cref="JiraAdfRenderer"/>, search in <see cref="JiraIssueSearcher"/>, transitions in <see cref="JiraTransitioner"/>.
/// </summary>
public sealed class  JiraTicketProvider : ITicketProvider
{
    private readonly string _baseUrl;
    private readonly TicketProviderHttpClient _http;
    private readonly IAttachmentLoader _attachmentLoader;
    private readonly JiraFieldMapper _mapper;
    private readonly JiraCommentMapper _commentMapper = new();
    private readonly JiraIssueSearcher _searcher;
    private readonly IJiraDiscoveryJqlBuilder _jqlBuilder = new JiraDiscoveryJqlBuilder();
    private readonly JiraTransitioner _transitioner;
    private readonly string? _projectKey;
    private readonly string _doneStatus;
    private readonly string _closeTransitionName;
    private readonly ILogger<JiraTicketProvider> _logger;
    private readonly TrackerParentLink _parentLink;
    private readonly JiraTicketFinalizer _finalizer;
    private readonly JiraTicketCreator _creator;
    private readonly AgentSmith.Contracts.Models.Configuration.JiraEndpoints _endpoints;
    private readonly string _parentLinkType;

    public string ProviderType => "Jira";

    public JiraTicketProvider(
        JiraTicketConnection connection,
        HttpClient httpClient, JiraFieldMapper mapper,
        ILogger<JiraTicketProvider> logger,
        string? doneStatus = null, string? closeTransitionName = null)
    {
        _baseUrl = connection.BaseUrl.TrimEnd('/');
        _http = TicketProviderHttpClient.WithBasicAuth(httpClient, connection.Email, connection.ApiToken);
        _mapper = mapper;
        _projectKey = connection.ProjectKey;
        _doneStatus = doneStatus ?? "Done";
        _closeTransitionName = closeTransitionName ?? "Close";
        _logger = logger;
        _parentLink = new TrackerParentLink("Jira", logger);
        _endpoints = connection.ResolvedEndpoints;
        _parentLinkType = connection.ParentLinkType ?? JiraTicketConnection.DefaultParentLinkType;
        _attachmentLoader = new JiraAttachmentLoader(httpClient, logger);
        _searcher = new JiraIssueSearcher(_http, mapper, connection, logger);
        _transitioner = new JiraTransitioner(_http, _baseUrl, _endpoints, logger);
        _finalizer = new JiraTicketFinalizer(_doneStatus, UpdateStatusAsync, CloseAndReportAsync,
            (ticket, status, ct) => _transitioner.TransitionAsync(ticket, status, null, ct));
        _creator = new JiraTicketCreator(_http, _baseUrl, _endpoints.Create, _projectKey, logger);
    }

    // "Who am I": the cheapest authenticated call that proves the credentials and the site.
    private const string MyselfEndpoint = "/rest/api/3/myself";

    public async Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var _ = await _http.SendForJsonOrThrowAsync(
                HttpMethod.Get, $"{_baseUrl}{MyselfEndpoint}", null, cancellationToken);
            return ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Jira tracker probe failed for {BaseUrl}", _baseUrl);
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    public async Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}{_endpoints.IssueFor(ticketId.Value)}?fields=summary,description,status,labels,attachment,assignee,reporter";
        _logger.LogDebug("Jira GetTicket #{Ticket}: GET {Url}", ticketId.Value, url);
        using var doc = await _http.SendForJsonAsync(HttpMethod.Get, url, null, cancellationToken)
            ?? throw new TicketNotFoundException(ticketId);
        var ticket = _mapper.Map(ticketId, doc.RootElement);
        _logger.LogDebug("Jira GetTicket #{Ticket}: status={Status} labels={Count}",
            ticketId.Value, ticket.Status, ticket.Labels?.Count ?? 0);
        return ticket;
    }

    public Task<IReadOnlyList<Ticket>> ListByLifecycleStatusAsync(
        TicketLifecycleStatus status, CancellationToken cancellationToken)
        => _searcher.SearchAsync(
            $"labels = \"{LifecycleLabels.For(status)}\"",
            $"lifecycle={status}", cancellationToken);

    // Open-state discovery for the poller; routing and trigger_statuses gate downstream in TrackerPoller.
    public Task<IReadOnlyList<Ticket>> ListOpenAsync(CancellationToken cancellationToken)
        => _searcher.SearchAsync("statusCategory != Done", "open-discovery", cancellationToken);

    // p0283b: composed claimable discovery — the JQL builder pushes the per-project status
    // branches; the tag match stays in-process (JQL labels= is case-sensitive).
    public Task<IReadOnlyList<Ticket>> ListClaimableAsync(
        DiscoveryQuery query, CancellationToken cancellationToken)
        => _searcher.SearchAsync(_jqlBuilder.BuildJql(query), "claimable", cancellationToken);

    public async Task<IReadOnlyList<AttachmentRef>> GetAttachmentRefsAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}{_endpoints.IssueFor(ticketId.Value)}?fields=attachment";
        try
        {
            using var doc = await _http.SendForJsonOrThrowAsync(HttpMethod.Get, url, null, cancellationToken);
            return doc.RootElement.TryGetProperty("fields", out var fields)
                ? JiraAttachmentLoader.ParseRefs(fields)
                : [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Jira attachment-ref fetch failed for {TicketId} — continuing without attachments",
                ticketId.Value);
            return [];
        }
    }

    public async Task<IReadOnlyList<TicketImageAttachment>> DownloadImageAttachmentsAsync(
        TicketId ticketId, CancellationToken cancellationToken) =>
        await TicketImageAttachmentDownloader.DownloadAllAsync(
            await GetAttachmentRefsAsync(ticketId, cancellationToken),
            _attachmentLoader.DownloadAsync, cancellationToken);

    public Task<CreatedTicket> CreateAsync(
        string title, string description, IReadOnlyList<string> labels, string? kind,
        CancellationToken cancellationToken) =>
        _creator.CreateAsync(title, description, labels, kind, cancellationToken);

    // A missing link type or disabled linking is the site's refusal, and a Failed link.
    public Task<ParentLinkResult> LinkToParentAsync(
        CreatedTicket child, TicketId parent, CancellationToken cancellationToken) =>
        _parentLink.AttemptAsync(() => _http.SendAsync(
            HttpMethod.Post, $"{_baseUrl}{_endpoints.IssueLink}",
            new { type = new { name = _parentLinkType }, inwardIssue = new { key = parent.Value }, outwardIssue = new { key = child.Id.Value } },
            cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<TicketDocumentAttachment>> DownloadDocumentAttachmentsAsync(
        TicketId ticketId, CancellationToken cancellationToken) =>
        await TicketDocumentAttachmentDownloader.DownloadAllAsync(
            await GetAttachmentRefsAsync(ticketId, cancellationToken),
            _attachmentLoader.DownloadAsync, cancellationToken);

    // GET on the endpoint UpdateStatusAsync posts to. Transport failures propagate — FetchTicketHandler owns fail-soft.
    public async Task<IReadOnlyList<TicketComment>> GetCommentsAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        using var doc = await _http.SendForJsonOrThrowAsync(
            HttpMethod.Get, $"{_baseUrl}{_endpoints.CommentFor(ticketId.Value)}", null, cancellationToken);
        return _commentMapper.MapMany(doc.RootElement);
    }

    public Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken) =>
        _http.SendAsync(HttpMethod.Post,
            $"{_baseUrl}{_endpoints.CommentFor(ticketId.Value)}",
            JiraAdfRenderer.CommentBody(comment), cancellationToken);

    public Task CloseTicketAsync(TicketId ticketId, string resolution, CancellationToken cancellationToken)
        => CloseAndReportAsync(ticketId, resolution, cancellationToken);

    // Answers whether the close actually transitioned the issue — what finalize reports.
    private async Task<bool> CloseAndReportAsync(TicketId ticket, string resolution, CancellationToken ct)
    {
        await UpdateStatusAsync(ticket, resolution, ct);
        return await _transitioner.TransitionAsync(ticket, _doneStatus, _closeTransitionName, ct);
    }

    public Task TransitionToAsync(TicketId ticketId, string statusName, CancellationToken cancellationToken)
        => _transitioner.TransitionAsync(ticketId, statusName, null, cancellationToken);

    public Task<TicketFinalizeResult> FinalizeAsync(
        TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken)
        => _finalizer.FinalizeAsync(ticketId, comment, doneStatus, cancellationToken);
}
