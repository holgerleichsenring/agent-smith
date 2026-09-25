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

/// <summary>Thin Octokit orchestrator; mapping, listing and attachments live in their own types.</summary>
public sealed class GitHubTicketProvider : ITicketProvider
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly GitHubClient _client;
    private readonly GitHubAttachmentLoader _attachmentLoader;
    private readonly ITicketFieldMapper<Issue> _mapper;
    private readonly GitHubCommentMapper _commentMapper = new();
    private readonly GitHubIssueLister _lister;
    private readonly TicketLabelVocabulary _labels;
    private readonly ILogger _logger;
    private readonly TrackerParentLink _parentLink;
    private readonly GitHubTicketFinalizer _finalizer;

    public string ProviderType => "GitHub";

    public GitHubTicketProvider(
        GitHubTicketConnection connection, GitHubAttachmentLoader attachmentLoader,
        ITicketFieldMapper<Issue> mapper, ILogger<GitHubTicketProvider> logger)
    {
        (_owner, _repo) = ParseGitHubUrl(connection.RepoUrl);
        _labels = connection.ResolvedLabels;
        _client = new GitHubClient(new ProductHeaderValue("AgentSmith"))
        { Credentials = new Credentials(connection.Token) };
        _attachmentLoader = attachmentLoader;
        _mapper = mapper;
        _lister = new GitHubIssueLister(_client, connection, _mapper, logger);
        _logger = logger;
        _parentLink = new TrackerParentLink("GitHub", logger);
        _finalizer = new GitHubTicketFinalizer(UpdateStatusAsync, CloseTicketAsync, TransitionToAsync);
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
        _logger.LogDebug("GitHub GetTicket #{Ticket}: Issue.Get {Owner}/{Repo}#{Number}", ticketId.Value, _owner, _repo, n);
        try
        {
            var ticket = _mapper.Map(ticketId, await _client.Issue.Get(_owner, _repo, n));
            _logger.LogDebug("GitHub GetTicket #{Ticket}: status={Status} labels={Count}", ticketId.Value, ticket.Status, ticket.Labels?.Count ?? 0);
            return ticket;
        }
        catch (NotFoundException) { throw new TicketNotFoundException(ticketId); }
    }

    public async Task<IReadOnlyList<AttachmentRef>> GetAttachmentRefsAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        if (!TryParseIssueNumber(ticketId, out var n)) return [];
        try { return GitHubAttachmentLoader.ParseRefs((await _client.Issue.Get(_owner, _repo, n)).Body); } catch { return []; }
    }

    // Transport failures propagate — FetchTicketHandler owns fail-soft.
    public async Task<IReadOnlyList<TicketComment>> GetCommentsAsync(TicketId ticketId, CancellationToken cancellationToken)
    {
        if (!TryParseIssueNumber(ticketId, out var n)) return [];
        return _commentMapper.MapMany(await _client.Issue.Comment.GetAllForIssue(_owner, _repo, n));
    }

    public async Task<IReadOnlyList<TicketImageAttachment>> DownloadImageAttachmentsAsync(
        TicketId ticketId, CancellationToken cancellationToken) =>
        await TicketImageAttachmentDownloader.DownloadAllAsync(
            await GetAttachmentRefsAsync(ticketId, cancellationToken),
            _attachmentLoader.DownloadAsync, cancellationToken);

    // GitHub has no work-item kind: a status that is not open or closed is applied as a label.
    public async Task<CreatedTicket> CreateAsync(
        string title, string description, IReadOnlyList<string> labels, string? kind, CancellationToken cancellationToken)
    {
        var issue = await _client.Issue.Create(_owner, _repo, BuildNewIssue(title, description, labels));
        _logger.LogInformation("GitHub created issue #{Number} in {Owner}/{Repo}", issue.Number, _owner, _repo);
        return Created(issue);
    }

    // Labels are native and the add is idempotent, so it is sent whole with no read first.
    public async Task<bool> AddLabelAsync(TicketId ticketId, string label, CancellationToken ct)
    {
        if (!TryParseIssueNumber(ticketId, out var n)) return false;
        await _client.Issue.Labels.AddToIssue(_owner, _repo, n, [label]);
        return true;
    }

    // The database id rides along: a sub-issue link names the child by it, not by its number.
    internal static CreatedTicket Created(Issue issue) =>
        new(new TicketId(issue.Number.ToString()), issue.HtmlUrl) { NativeId = issue.Id.ToString() };

    public async Task<ParentLinkResult> LinkToParentAsync(CreatedTicket child, TicketId parent, CancellationToken cancellationToken)
    {
        if (!TryParseIssueNumber(parent, out var n) || !long.TryParse(child.NativeId, out var childId))
            return ParentLinkResult.Failed("A sub-issue link needs the parent's number and the child's database id.");
        var request = GitHubSubIssueRequest.For(_owner, _repo, n, childId);
        return await _parentLink.AttemptAsync(() =>
            _client.Connection.Post(request.Path, request.Body, GitHubSubIssueRequest.Accepts, cancellationToken), cancellationToken);
    }

    internal static NewIssue BuildNewIssue(string title, string description, IReadOnlyList<string> labels)
    {
        var issue = new NewIssue(title) { Body = description };
        foreach (var label in labels) issue.Labels.Add(label);
        return issue;
    }

    public async Task<IReadOnlyList<TicketDocumentAttachment>> DownloadDocumentAttachmentsAsync(TicketId ticketId, CancellationToken cancellationToken) =>
        await TicketDocumentAttachmentDownloader.DownloadAllAsync(
            await GetAttachmentRefsAsync(ticketId, cancellationToken),
            _attachmentLoader.DownloadAsync, cancellationToken);

    public async Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken)
    {
        if (TryParseIssueNumber(ticketId, out var n))
            await _client.Issue.Comment.Create(_owner, _repo, n, comment);
    }

    // 2026-09-22-9519: the state write's own answer, which reports an unparseable id as false.
    public async Task<bool> CloseTicketAsync(TicketId ticketId, string resolution, CancellationToken cancellationToken)
    {
        await UpdateStatusAsync(ticketId, resolution, cancellationToken);
        return await TransitionToAsync(ticketId, "closed", cancellationToken);
    }

    public Task<IReadOnlyList<Ticket>> ListOpenAsync(CancellationToken cancellationToken)
        => _lister.ListOpenAsync(cancellationToken);

    // Issues are open/closed only: narrow by label when every branch is tag-based, else stay broad.
    // The trigger-label guard stays in-process — GitHub's label filter is AND-only with no prefix match.
    public Task<IReadOnlyList<Ticket>> ListClaimableAsync(
        DiscoveryQuery query, CancellationToken cancellationToken)
        => query.AllTagLabelsOrNull() is { Count: > 0 } labels
            ? _lister.ListByLabelsAsync(labels, ItemStateFilter.Open, "claimable", cancellationToken)
            : ListOpenAsync(cancellationToken);

    public Task<IReadOnlyList<Ticket>> ListByLifecycleStatusAsync(
        TicketLifecycleStatus status, CancellationToken cancellationToken)
        => _lister.ListByLabelsAsync(
            [_labels.For(status)], ItemStateFilter.All, $"lifecycle={status}", cancellationToken);

    public async Task<bool> TransitionToAsync(TicketId ticketId, string statusName, CancellationToken cancellationToken)
    {
        if (!TryParseIssueNumber(ticketId, out var n)) return false;
        if (statusName.Equals("closed", StringComparison.OrdinalIgnoreCase))
            await _client.Issue.Update(_owner, _repo, n, new IssueUpdate { State = ItemState.Closed });
        else if (statusName.Equals("open", StringComparison.OrdinalIgnoreCase))
            await _client.Issue.Update(_owner, _repo, n, new IssueUpdate { State = ItemState.Open });
        else await _client.Issue.Labels.AddToIssue(_owner, _repo, n, [statusName]);
        return true;
    }

    public Task<TicketFinalizeResult> FinalizeAsync(
        TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken)
        => _finalizer.FinalizeAsync(ticketId, comment, doneStatus, cancellationToken);

    private static bool TryParseIssueNumber(TicketId id, out int n) => int.TryParse(id.Value, out n);

    private static (string owner, string repo) ParseGitHubUrl(string url)
    {
        var segments = new Uri(url).AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2) throw new ConfigurationException($"Invalid GitHub URL: {url}");
        return (segments[0], segments[1]);
    }
}
