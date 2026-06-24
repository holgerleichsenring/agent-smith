using System.Net.Http;
using AgentSmith.Contracts.Models;
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
    }

    public async Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        var doc = await _http.SendForJsonAsync(HttpMethod.Get, IssueUrl(ticketId), null, cancellationToken)
            ?? throw new TicketNotFoundException(ticketId);
        using (doc) return _mapper.Map(ticketId, doc.RootElement);
    }

    // Open-state discovery for the poller + dashboard/chat listing. Without this
    // GitLab fell back to ITicketProvider's empty default (see JiraTicketProvider).
    public Task<IReadOnlyList<Ticket>> ListOpenAsync(CancellationToken cancellationToken)
        => _lister.ListOpenAsync(cancellationToken);

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
