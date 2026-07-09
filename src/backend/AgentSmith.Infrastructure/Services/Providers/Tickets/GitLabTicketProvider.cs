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

/// <summary>
/// p0147f: thin GitLab REST v4 orchestrator. Field mapping in
/// <see cref="GitLabFieldMapper"/>; list/query in <see cref="GitLabIssueLister"/>;
/// auth + send-or-throw in <see cref="TicketProviderHttpClient"/>;
/// attachments in <see cref="GitLabAttachmentLoader"/>.
/// </summary>
public sealed class GitLabTicketProvider : ITicketProvider
{
    private readonly string _baseUrl;
    private readonly string _projectPath;
    private readonly string _privateToken;
    private readonly HttpClient _httpClient;
    private readonly TicketProviderHttpClient _http;
    private readonly GitLabAttachmentLoader _attachmentLoader;
    private readonly GitLabFieldMapper _mapper;
    private readonly GitLabIssueLister _lister;
    private readonly ILogger _logger;

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

    // Open-state discovery for the poller + dashboard/chat listing. Without this
    // GitLab fell back to ITicketProvider's empty default (see JiraTicketProvider).
    public Task<IReadOnlyList<Ticket>> ListOpenAsync(CancellationToken cancellationToken)
        => _lister.ListOpenAsync(cancellationToken);

    // p0283b: GitLab issues are opened/closed only, so the status branch maps to "opened";
    // narrow by the resolution tag (opened + labels=) when every branch is Tag-based, else broad.
    // p0300c: the agent-smith trigger-label guard (query.TriggerLabels) stays in-process — the
    // GitLab ?labels= param is AND-only with no prefix match, and issues are repo-scoped (bounded),
    // so the in-process ProjectResolver drops non-trigger tickets without a server-side clause.
    public Task<IReadOnlyList<Ticket>> ListClaimableAsync(
        DiscoveryQuery query, CancellationToken cancellationToken)
        => query.AllTagLabelsOrNull() is { Count: > 0 } labels
            ? _lister.SearchAsync(labels, "claimable", cancellationToken)
            : ListOpenAsync(cancellationToken);

    public Task<IReadOnlyList<Ticket>> ListByLifecycleStatusAsync(
        TicketLifecycleStatus status, CancellationToken cancellationToken)
        => _lister.SearchAsync([LifecycleLabels.For(status)], $"lifecycle={status}", cancellationToken);

    public async Task<IReadOnlyList<AttachmentRef>> GetAttachmentRefsAsync(
        TicketId ticketId, CancellationToken cancellationToken)
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

    public async Task<IReadOnlyList<TicketImageAttachment>> DownloadImageAttachmentsAsync(
        TicketId ticketId, CancellationToken cancellationToken) =>
        await TicketImageAttachmentDownloader.DownloadAllAsync(
            await GetAttachmentRefsAsync(ticketId, cancellationToken),
            _attachmentLoader.DownloadAsync, cancellationToken);

    public async Task<CreatedTicket> CreateAsync(
        string title, string description, IReadOnlyList<string> labels, CancellationToken cancellationToken)
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

    public Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken) =>
        _http.SendAsync(HttpMethod.Post,
            $"{_baseUrl}/api/v4/projects/{_projectPath}/issues/{ticketId.Value}/notes",
            new { body = comment }, cancellationToken);

    public async Task CloseTicketAsync(TicketId ticketId, string resolution, CancellationToken cancellationToken)
    {
        await UpdateStatusAsync(ticketId, resolution, cancellationToken);
        await _http.SendAsync(HttpMethod.Put, IssueUrl(ticketId),
            new { state_event = "close" }, cancellationToken);
    }

    public Task TransitionToAsync(TicketId ticketId, string statusName, CancellationToken cancellationToken) =>
        _http.SendAsync(HttpMethod.Put, IssueUrl(ticketId),
            new { state_event = ToStateEvent(statusName) }, cancellationToken);

    // GitLab issues have no rev-guard; sequential note + state change is safe.
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

    private string IssueUrl(TicketId ticketId) =>
        $"{_baseUrl}/api/v4/projects/{_projectPath}/issues/{ticketId.Value}";

    private static string ToStateEvent(string statusName) => statusName.ToLowerInvariant() switch
    {
        "closed" => "close",
        "opened" or "open" or "reopen" => "reopen",
        var s => s
    };
}
