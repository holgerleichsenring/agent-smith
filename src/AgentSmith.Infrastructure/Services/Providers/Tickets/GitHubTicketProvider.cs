using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: thin Octokit orchestrator. Field mapping in
/// <see cref="GitHubFieldMapper"/>; list/query in <see cref="GitHubIssueLister"/>;
/// attachments in <see cref="GitHubAttachmentLoader"/>.
/// </summary>
public sealed class GitHubTicketProvider : ITicketProvider
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly GitHubClient _client;
    private readonly GitHubAttachmentLoader _attachmentLoader;
    private readonly ITicketFieldMapper<Issue> _mapper;
    private readonly GitHubIssueLister _lister;

    public string ProviderType => "GitHub";

    public GitHubTicketProvider(
        string repoUrl, string token, GitHubAttachmentLoader attachmentLoader,
        ITicketFieldMapper<Issue> mapper, ILogger<GitHubTicketProvider> logger)
    {
        (_owner, _repo) = ParseGitHubUrl(repoUrl);
        _client = new GitHubClient(new ProductHeaderValue("AgentSmith"))
        { Credentials = new Credentials(token) };
        _attachmentLoader = attachmentLoader;
        _mapper = mapper;
        _lister = new GitHubIssueLister(_client, _owner, _repo, _mapper, logger);
    }

    public async Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        if (!TryParseIssueNumber(ticketId, out var n)) throw new TicketNotFoundException(ticketId);
        try { return _mapper.Map(ticketId, await _client.Issue.Get(_owner, _repo, n)); }
        catch (NotFoundException) { throw new TicketNotFoundException(ticketId); }
    }

    public async Task<IReadOnlyList<AttachmentRef>> GetAttachmentRefsAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        if (!TryParseIssueNumber(ticketId, out var n)) return [];
        try { return GitHubAttachmentLoader.ParseRefs((await _client.Issue.Get(_owner, _repo, n)).Body); }
        catch { return []; }
    }

    public async Task<IReadOnlyList<TicketImageAttachment>> DownloadImageAttachmentsAsync(
        TicketId ticketId, CancellationToken cancellationToken) =>
        await TicketImageAttachmentDownloader.DownloadAllAsync(
            await GetAttachmentRefsAsync(ticketId, cancellationToken),
            _attachmentLoader.DownloadAsync, cancellationToken);

    public async Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken)
    {
        if (TryParseIssueNumber(ticketId, out var n))
            await _client.Issue.Comment.Create(_owner, _repo, n, comment);
    }

    public async Task CloseTicketAsync(TicketId ticketId, string resolution, CancellationToken cancellationToken)
    {
        if (!TryParseIssueNumber(ticketId, out var n)) return;
        await _client.Issue.Comment.Create(_owner, _repo, n, resolution);
        await _client.Issue.Update(_owner, _repo, n, new IssueUpdate { State = ItemState.Closed });
    }

    public Task<IReadOnlyList<Ticket>> ListByLifecycleStatusAsync(
        TicketLifecycleStatus status, CancellationToken cancellationToken)
        => _lister.ListByLabelsAsync(
            [LifecycleLabels.For(status)], ItemStateFilter.All, $"lifecycle={status}", cancellationToken);

    public Task<IReadOnlyList<Ticket>> ListByLabelsInOpenStatesAsync(
        IReadOnlyCollection<string> labels, CancellationToken cancellationToken)
        => labels.Count == 0
            ? Task.FromResult<IReadOnlyList<Ticket>>([])
            : _lister.ListByLabelsAsync(
                labels, ItemStateFilter.Open, $"labels=[{string.Join(", ", labels)}]", cancellationToken);

    public async Task TransitionToAsync(TicketId ticketId, string statusName, CancellationToken cancellationToken)
    {
        if (!TryParseIssueNumber(ticketId, out var n)) return;
        // "closed"/"open" are native states; anything else is treated as a label.
        if (statusName.Equals("closed", StringComparison.OrdinalIgnoreCase))
            await _client.Issue.Update(_owner, _repo, n, new IssueUpdate { State = ItemState.Closed });
        else if (statusName.Equals("open", StringComparison.OrdinalIgnoreCase))
            await _client.Issue.Update(_owner, _repo, n, new IssueUpdate { State = ItemState.Open });
        else
            await _client.Issue.Labels.AddToIssue(_owner, _repo, n, [statusName]);
    }

    private static bool TryParseIssueNumber(TicketId id, out int n) => int.TryParse(id.Value, out n);

    private static (string owner, string repo) ParseGitHubUrl(string url)
    {
        var segments = new Uri(url).AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2) throw new ConfigurationException($"Invalid GitHub URL: {url}");
        return (segments[0], segments[1]);
    }
}
