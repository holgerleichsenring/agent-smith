using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.ValueObjects;
using Octokit;

namespace AgentSmith.Infrastructure.Providers.Tickets;

/// <summary>
/// Fetches issues from GitHub.
/// </summary>
public sealed class GitHubTicketProvider : ITicketProvider
{
    private readonly string _owner;
    private readonly string _repo;
    private readonly GitHubClient _client;

    public string ProviderType => "GitHub";

    public GitHubTicketProvider(string repoUrl, string token)
    {
        (_owner, _repo) = ParseGitHubUrl(repoUrl);
        _client = CreateClient(token);
    }

    public async Task<Ticket> GetTicketAsync(
        TicketId ticketId, CancellationToken cancellationToken = default)
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
        return new Ticket(
            ticketId,
            issue.Title,
            issue.Body ?? "",
            null,
            issue.State.StringValue,
            "GitHub");
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
