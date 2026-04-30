using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Fetches issues from GitHub.
/// </summary>
public sealed class GitHubTicketProvider : ITicketProvider
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly GitHubClient _client;
    private readonly GitHubAttachmentLoader _attachmentLoader;
    private readonly ILogger<GitHubTicketProvider> _logger;

    public string ProviderType => "GitHub";

    public GitHubTicketProvider(
        string repoUrl, string token, GitHubAttachmentLoader attachmentLoader,
        ILogger<GitHubTicketProvider> logger)
    {
        (_owner, _repo) = ParseGitHubUrl(repoUrl);
        _client = CreateClient(token);
        _attachmentLoader = attachmentLoader;
        _logger = logger;
    }

    public async Task<Ticket> GetTicketAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(ticketId.Value, out var issueNumber))
            throw new TicketNotFoundException(ticketId);

        try
        {
            var issue = await _client.Issue.Get(_owner, _repo, issueNumber);
            return MapToTicket(ticketId, issue);
        }
        catch (NotFoundException)
        {
            throw new TicketNotFoundException(ticketId);
        }
    }

    private static Ticket MapToTicket(TicketId ticketId, Issue issue)
    {
        var labels = issue.Labels?.Select(l => l.Name).ToList() ?? [];
        return new Ticket(
            ticketId,
            issue.Title,
            issue.Body ?? "",
            null,
            issue.State.StringValue,
            "GitHub",
            labels);
    }

    public async Task<IReadOnlyList<AttachmentRef>> GetAttachmentRefsAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        if (!int.TryParse(ticketId.Value, out var issueNumber))
            return [];

        try
        {
            var issue = await _client.Issue.Get(_owner, _repo, issueNumber);
            return GitHubAttachmentLoader.ParseRefs(issue.Body);
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<TicketImageAttachment>> DownloadImageAttachmentsAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        var refs = await GetAttachmentRefsAsync(ticketId, cancellationToken);
        if (refs.Count == 0) return [];

        var results = new List<TicketImageAttachment>();
        foreach (var r in refs)
        {
            var content = await _attachmentLoader.DownloadAsync(r, cancellationToken);
            if (content is not null)
                results.Add(new TicketImageAttachment(r, content));
        }
        return results;
    }

    public async Task UpdateStatusAsync(
        TicketId ticketId, string comment, CancellationToken cancellationToken)
    {
        if (!int.TryParse(ticketId.Value, out var issueNumber))
            return;

        await _client.Issue.Comment.Create(_owner, _repo, issueNumber, comment);
    }

    public async Task CloseTicketAsync(
        TicketId ticketId, string resolution, CancellationToken cancellationToken)
    {
        if (!int.TryParse(ticketId.Value, out var issueNumber))
            return;

        await _client.Issue.Comment.Create(_owner, _repo, issueNumber, resolution);
        await _client.Issue.Update(_owner, _repo, issueNumber,
            new IssueUpdate { State = ItemState.Closed });
    }

    public async Task<IReadOnlyList<Ticket>> ListByLifecycleStatusAsync(
        TicketLifecycleStatus status, CancellationToken cancellationToken)
    {
        var label = LifecycleLabels.For(status);
        _logger.LogInformation(
            "GitHub ListByLifecycleStatus: repo={Owner}/{Repo} status={Status} (label '{Label}')",
            _owner, _repo, status, label);
        try
        {
            var request = new RepositoryIssueRequest
            {
                State = ItemStateFilter.All,
                Labels = { label }
            };
            var issues = await _client.Issue.GetAllForRepository(_owner, _repo, request);
            var tickets = issues.Select(i => MapToTicket(new TicketId(i.Number.ToString()), i)).ToList();
            _logger.LogInformation("GitHub ListByLifecycleStatus: returned {Count} ticket(s)", tickets.Count);
            return tickets;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "GitHub ListByLifecycleStatus failed for {Owner}/{Repo} status={Status}",
                _owner, _repo, status);
            return [];
        }
    }

    public async Task<IReadOnlyList<Ticket>> ListByLabelsInOpenStatesAsync(
        IReadOnlyCollection<string> labels, CancellationToken cancellationToken)
    {
        if (labels.Count == 0) return [];
        _logger.LogInformation(
            "GitHub ListByLabelsInOpenStates: repo={Owner}/{Repo} labels=[{Labels}]",
            _owner, _repo, string.Join(", ", labels));
        try
        {
            // Octokit's RepositoryIssueRequest ANDs the labels list. For OR-semantics we
            // query each label separately and dedupe by issue number.
            var deduped = new Dictionary<int, Ticket>();
            foreach (var label in labels)
            {
                var req = new RepositoryIssueRequest { State = ItemStateFilter.Open };
                req.Labels.Add(label);
                var issues = await _client.Issue.GetAllForRepository(_owner, _repo, req);
                foreach (var issue in issues)
                    deduped[issue.Number] = MapToTicket(new TicketId(issue.Number.ToString()), issue);
            }
            _logger.LogInformation(
                "GitHub ListByLabelsInOpenStates: returned {Count} ticket(s)", deduped.Count);
            return [.. deduped.Values];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "GitHub ListByLabelsInOpenStates failed for {Owner}/{Repo} labels=[{Labels}]",
                _owner, _repo, string.Join(", ", labels));
            return [];
        }
    }

    public async Task TransitionToAsync(
        TicketId ticketId, string statusName, CancellationToken cancellationToken)
    {
        if (!int.TryParse(ticketId.Value, out var issueNumber))
            return;

        // GitHub Issues: "closed" transitions to closed, anything else reopens
        if (statusName.Equals("closed", StringComparison.OrdinalIgnoreCase))
        {
            await _client.Issue.Update(_owner, _repo, issueNumber,
                new IssueUpdate { State = ItemState.Closed });
        }
        else if (statusName.Equals("open", StringComparison.OrdinalIgnoreCase))
        {
            await _client.Issue.Update(_owner, _repo, issueNumber,
                new IssueUpdate { State = ItemState.Open });
        }
        // For label-based "status" transitions, add/remove label
        else
        {
            await _client.Issue.Labels.AddToIssue(_owner, _repo, issueNumber, [statusName]);
        }
    }

    private static (string owner, string repo) ParseGitHubUrl(string url)
    {
        var uri = new Uri(url);
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2)
            throw new ConfigurationException($"Invalid GitHub URL: {url}");

        return (segments[0], segments[1]);
    }

    private static GitHubClient CreateClient(string token)
    {
        var client = new GitHubClient(new ProductHeaderValue("AgentSmith"));
        client.Credentials = new Credentials(token);
        return client;
    }
}
