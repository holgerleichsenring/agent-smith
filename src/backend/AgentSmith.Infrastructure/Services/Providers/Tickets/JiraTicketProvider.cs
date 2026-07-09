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
/// p0147f: thin Jira Cloud REST v3 orchestrator. Field mapping in
/// <see cref="JiraFieldMapper"/>; ADF body in <see cref="JiraAdfRenderer"/>;
/// search in <see cref="JiraIssueSearcher"/>; workflow transitions in
/// <see cref="JiraTransitioner"/>; attachments in
/// <see cref="JiraAttachmentLoader"/>.
/// </summary>
public sealed class  JiraTicketProvider : ITicketProvider
{
    private readonly string _baseUrl;
    private readonly TicketProviderHttpClient _http;
    private readonly IAttachmentLoader _attachmentLoader;
    private readonly JiraFieldMapper _mapper;
    private readonly JiraIssueSearcher _searcher;
    private readonly IJiraDiscoveryJqlBuilder _jqlBuilder = new JiraDiscoveryJqlBuilder();
    private readonly JiraTransitioner _transitioner;
    private readonly string? _projectKey;
    private readonly string _doneStatus;
    private readonly string _closeTransitionName;
    private readonly ILogger<JiraTicketProvider> _logger;
    private readonly AgentSmith.Contracts.Models.Configuration.JiraEndpoints _endpoints;

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
        _endpoints = connection.ResolvedEndpoints;
        _attachmentLoader = new JiraAttachmentLoader(httpClient, logger);
        _searcher = new JiraIssueSearcher(_http, mapper, connection, logger);
        _transitioner = new JiraTransitioner(_http, _baseUrl, _endpoints, logger);
    }

    // Canonical Jira Cloud "who am I" endpoint — the cheapest authenticated call
    // that proves the email + API token are valid and the site is reachable.
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
        var url = $"{_baseUrl}{_endpoints.IssueFor(ticketId.Value)}?fields=summary,description,status,attachment";
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

    // Open-state discovery query for the poller. Without this Jira fell back to
    // ITicketProvider's empty default, so the poller only ever saw lifecycle-tagged
    // tickets and NEVER discovered a fresh ticket — only AzDO implemented ListOpenAsync.
    // "Open" = statusCategory != Done; per-ticket routing + trigger_statuses gating
    // run downstream in TrackerPoller.
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

    // Issue type "Task" is the one type present in every Jira project template;
    // the description travels as line-preserving ADF so it reads back intact.
    public async Task<CreatedTicket> CreateAsync(
        string title, string description, IReadOnlyList<string> labels, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_projectKey))
            throw new ConfigurationException(
                "Jira ticket creation requires a project key on the tracker connection.");
        var body = new
        {
            fields = new
            {
                project = new { key = _projectKey },
                summary = title,
                issuetype = new { name = "Task" },
                description = JiraAdfRenderer.FromMultilineText(description),
                labels,
            },
        };
        using var doc = await _http.SendForJsonOrThrowAsync(
            HttpMethod.Post, $"{_baseUrl}{_endpoints.Create}", body, cancellationToken);
        var key = doc.RootElement.GetProperty("key").GetString()
            ?? throw new InvalidOperationException("Jira returned a created issue without a key.");
        _logger.LogInformation("Jira created issue {Key} in project {Project}", key, _projectKey);
        return new CreatedTicket(new TicketId(key), $"{_baseUrl}/browse/{key}");
    }

    public Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken) =>
        _http.SendAsync(HttpMethod.Post,
            $"{_baseUrl}{_endpoints.CommentFor(ticketId.Value)}",
            JiraAdfRenderer.CommentBody(comment), cancellationToken);

    public async Task CloseTicketAsync(TicketId ticketId, string resolution, CancellationToken cancellationToken)
    {
        await UpdateStatusAsync(ticketId, resolution, cancellationToken);
        await _transitioner.TransitionAsync(ticketId, _doneStatus, _closeTransitionName, cancellationToken);
    }

    public Task TransitionToAsync(TicketId ticketId, string statusName, CancellationToken cancellationToken)
        => _transitioner.TransitionAsync(ticketId, statusName, null, cancellationToken);

    // Jira issues have no rev-guard on comments + transitions; sequential is safe.
    public async Task FinalizeAsync(
        TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(doneStatus))
            await CloseTicketAsync(ticketId, comment, cancellationToken);
        else
        {
            await UpdateStatusAsync(ticketId, comment, cancellationToken);
            await TransitionToAsync(ticketId, doneStatus, cancellationToken);
        }
    }
}
