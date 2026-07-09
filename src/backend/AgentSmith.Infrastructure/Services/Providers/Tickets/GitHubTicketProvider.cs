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
    private readonly GitHubCommentMapper _commentMapper = new();
    private readonly GitHubIssueLister _lister;
    private readonly ILogger _logger;

    public string ProviderType => "GitHub";

    public GitHubTicketProvider(
        GitHubTicketConnection connection, GitHubAttachmentLoader attachmentLoader,
        ITicketFieldMapper<Issue> mapper, ILogger<GitHubTicketProvider> logger)
    {
        (_owner, _repo) = ParseGitHubUrl(connection.RepoUrl);
        _client = new GitHubClient(new ProductHeaderValue("AgentSmith"))
        { Credentials = new Credentials(connection.Token) };
        _attachmentLoader = attachmentLoader;
        _mapper = mapper;
        _lister = new GitHubIssueLister(_client, connection, _mapper, logger);
        _logger = logger;
    }

    public async Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _client.Repository.Get(_owner, _repo);
            return ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GitHub tracker probe failed for {Owner}/{Repo}", _owner, _repo);
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    public async Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        if (!TryParseIssueNumber(ticketId, out var n)) throw new TicketNotFoundException(ticketId);
        _logger.LogDebug("GitHub GetTicket #{Ticket}: Issue.Get {Owner}/{Repo}#{Number}",
            ticketId.Value, _owner, _repo, n);
        try
        {
            var ticket = _mapper.Map(ticketId, await _client.Issue.Get(_owner, _repo, n));
            _logger.LogDebug("GitHub GetTicket #{Ticket}: status={Status} labels={Count}",
                ticketId.Value, ticket.Status, ticket.Labels?.Count ?? 0);
            return ticket;
        }
        catch (NotFoundException) { throw new TicketNotFoundException(ticketId); }
    }

    public async Task<IReadOnlyList<AttachmentRef>> GetAttachmentRefsAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        if (!TryParseIssueNumber(ticketId, out var n)) return [];
        try { return GitHubAttachmentLoader.ParseRefs((await _client.Issue.Get(_owner, _repo, n)).Body); }
        catch { return []; }
    }

    // p0317: the ticket conversation. Transport failures propagate — the
    // fetch-time caller (FetchTicketHandler) owns the fail-soft contract.
    public async Task<IReadOnlyList<TicketComment>> GetCommentsAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        if (!TryParseIssueNumber(ticketId, out var n)) return [];
        return _commentMapper.MapMany(await _client.Issue.Comment.GetAllForIssue(_owner, _repo, n));
    }

    public async Task<IReadOnlyList<TicketImageAttachment>> DownloadImageAttachmentsAsync(
        TicketId ticketId, CancellationToken cancellationToken) =>
        await TicketImageAttachmentDownloader.DownloadAllAsync(
            await GetAttachmentRefsAsync(ticketId, cancellationToken),
            _attachmentLoader.DownloadAsync, cancellationToken);

    public async Task<CreatedTicket> CreateAsync(
        string title, string description, IReadOnlyList<string> labels, CancellationToken cancellationToken)
    {
        var issue = await _client.Issue.Create(_owner, _repo, BuildNewIssue(title, description, labels));
        _logger.LogInformation(
            "GitHub created issue #{Number} in {Owner}/{Repo}", issue.Number, _owner, _repo);
        return new CreatedTicket(new TicketId(issue.Number.ToString()), issue.HtmlUrl);
    }

    internal static NewIssue BuildNewIssue(
        string title, string description, IReadOnlyList<string> labels)
    {
        var issue = new NewIssue(title) { Body = description };
        foreach (var label in labels) issue.Labels.Add(label);
        return issue;
    }

    public async Task<IReadOnlyList<TicketDocumentAttachment>> DownloadDocumentAttachmentsAsync(
        TicketId ticketId, CancellationToken cancellationToken) =>
        await TicketDocumentAttachmentDownloader.DownloadAllAsync(
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

    // Open-state discovery for the poller + dashboard/chat listing. Without this
    // GitHub fell back to ITicketProvider's empty default (see JiraTicketProvider).
    public Task<IReadOnlyList<Ticket>> ListOpenAsync(CancellationToken cancellationToken)
        => _lister.ListOpenAsync(cancellationToken);

    // p0283b: GitHub issues are open/closed only, so the status branch maps to "open"; narrow
    // by the resolution tag (open + label) when every branch is Tag-based, else stay broad.
    // p0300c: the agent-smith trigger-label guard (query.TriggerLabels) stays in-process — the
    // GitHub label filter is AND-only with no prefix match, and issues are repo-scoped (bounded),
    // so the in-process ProjectResolver drops non-trigger tickets without a server-side clause.
    public Task<IReadOnlyList<Ticket>> ListClaimableAsync(
        DiscoveryQuery query, CancellationToken cancellationToken)
        => query.AllTagLabelsOrNull() is { Count: > 0 } labels
            ? _lister.ListByLabelsAsync(labels, ItemStateFilter.Open, "claimable", cancellationToken)
            : ListOpenAsync(cancellationToken);

    public Task<IReadOnlyList<Ticket>> ListByLifecycleStatusAsync(
        TicketLifecycleStatus status, CancellationToken cancellationToken)
        => _lister.ListByLabelsAsync(
            [LifecycleLabels.For(status)], ItemStateFilter.All, $"lifecycle={status}", cancellationToken);

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

    // GitHub issues have no rev-guard; sequential comment + state change is safe.
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

    private static bool TryParseIssueNumber(TicketId id, out int n) => int.TryParse(id.Value, out n);

    private static (string owner, string repo) ParseGitHubUrl(string url)
    {
        var segments = new Uri(url).AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2) throw new ConfigurationException($"Invalid GitHub URL: {url}");
        return (segments[0], segments[1]);
    }
}
