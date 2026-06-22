using AgentSmith.Contracts.Models;
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
public sealed class JiraTicketProvider : ITicketProvider
{
    private readonly string _baseUrl;
    private readonly TicketProviderHttpClient _http;
    private readonly IAttachmentLoader _attachmentLoader;
    private readonly JiraFieldMapper _mapper;
    private readonly JiraIssueSearcher _searcher;
    private readonly JiraTransitioner _transitioner;
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
        _doneStatus = doneStatus ?? "Done";
        _closeTransitionName = closeTransitionName ?? "Close";
        _logger = logger;
        _endpoints = connection.ResolvedEndpoints;
        _attachmentLoader = new JiraAttachmentLoader(httpClient, logger);
        _searcher = new JiraIssueSearcher(_http, mapper, connection, logger);
        _transitioner = new JiraTransitioner(_http, _baseUrl, _endpoints, logger);
    }

    public async Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}{_endpoints.IssueFor(ticketId.Value)}?fields=summary,description,status,attachment";
        using var doc = await _http.SendForJsonAsync(HttpMethod.Get, url, null, cancellationToken)
            ?? throw new TicketNotFoundException(ticketId);
        return _mapper.Map(ticketId, doc.RootElement);
    }

    public Task<IReadOnlyList<Ticket>> ListByLifecycleStatusAsync(
        TicketLifecycleStatus status, CancellationToken cancellationToken)
        => _searcher.SearchAsync(
            $"labels = \"{LifecycleLabels.For(status)}\"",
            $"lifecycle={status}", cancellationToken);

    public Task<IReadOnlyList<Ticket>> ListByLabelsInOpenStatesAsync(
        IReadOnlyCollection<string> labels, CancellationToken cancellationToken)
    {
        if (labels.Count == 0) return Task.FromResult<IReadOnlyList<Ticket>>([]);
        var quoted = string.Join(", ", labels.Select(l => $"\"{l.Replace("\"", "\\\"")}\""));
        return _searcher.SearchAsync(
            $"labels in ({quoted}) AND statusCategory != Done",
            $"labels=[{string.Join(", ", labels)}]", cancellationToken);
    }

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
