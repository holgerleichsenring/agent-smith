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

    public string ProviderType => "Jira";

    public JiraTicketProvider(
        string baseUrl, string email, string apiToken,
        HttpClient httpClient, JiraFieldMapper mapper,
        ILogger<JiraTicketProvider> logger,
        string? doneStatus = null, string? closeTransitionName = null, string? projectKey = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _http = TicketProviderHttpClient.WithBasicAuth(httpClient, email, apiToken);
        _mapper = mapper;
        _doneStatus = doneStatus ?? "Done";
        _closeTransitionName = closeTransitionName ?? "Close";
        _attachmentLoader = new JiraAttachmentLoader(httpClient, logger);
        _searcher = new JiraIssueSearcher(_http, mapper, _baseUrl, projectKey, logger);
        _transitioner = new JiraTransitioner(_http, _baseUrl, logger);
    }

    public async Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}/rest/api/3/issue/{ticketId.Value}?fields=summary,description,status,attachment";
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
        var url = $"{_baseUrl}/rest/api/3/issue/{ticketId.Value}?fields=attachment";
        using var doc = await _http.TrySendForJsonAsync(HttpMethod.Get, url, null, cancellationToken);
        if (doc is null || !doc.RootElement.TryGetProperty("fields", out var fields)) return [];
        return JiraAttachmentLoader.ParseRefs(fields);
    }

    public async Task<IReadOnlyList<TicketImageAttachment>> DownloadImageAttachmentsAsync(
        TicketId ticketId, CancellationToken cancellationToken) =>
        await TicketImageAttachmentDownloader.DownloadAllAsync(
            await GetAttachmentRefsAsync(ticketId, cancellationToken),
            _attachmentLoader.DownloadAsync, cancellationToken);

    public Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken) =>
        _http.SendAsync(HttpMethod.Post,
            $"{_baseUrl}/rest/api/3/issue/{ticketId.Value}/comment",
            JiraAdfRenderer.CommentBody(comment), cancellationToken);

    public async Task CloseTicketAsync(TicketId ticketId, string resolution, CancellationToken cancellationToken)
    {
        await UpdateStatusAsync(ticketId, resolution, cancellationToken);
        await _transitioner.TransitionAsync(ticketId, _doneStatus, _closeTransitionName, cancellationToken);
    }

    public Task TransitionToAsync(TicketId ticketId, string statusName, CancellationToken cancellationToken)
        => _transitioner.TransitionAsync(ticketId, statusName, null, cancellationToken);
}
