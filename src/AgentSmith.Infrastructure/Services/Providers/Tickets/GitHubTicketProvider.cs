using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
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

    public string ProviderType => "GitHub";

    public GitHubTicketProvider(string repoUrl, string token, GitHubAttachmentLoader attachmentLoader)
    {
        (_owner, _repo) = ParseGitHubUrl(repoUrl);
        _client = CreateClient(token);
        _attachmentLoader = attachmentLoader;
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
        try
        {
            var request = new RepositoryIssueRequest
            {
                State = ItemStateFilter.All,
                Labels = { label }
            };
            var issues = await _client.Issue.GetAllForRepository(_owner, _repo, request);
            return issues.Select(i => MapToTicket(new TicketId(i.Number.ToString()), i)).ToList();
        }
        catch
        {
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
