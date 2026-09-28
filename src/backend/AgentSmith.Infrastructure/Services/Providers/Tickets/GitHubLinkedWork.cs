using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-28-1da5c: the pull requests GitHub cross-references from an issue, read off its timeline.
/// <para>
/// BRANCHES ARE NOT REACHABLE. GitHub's linked branches are a GraphQL concept and the client this
/// tree speaks is REST, so this answers pull requests and says nothing about branches rather than
/// answering "none" — which would be a claim about the repository this cannot see.
/// </para>
/// </summary>
public sealed class GitHubLinkedWork : ITicketLinkedWork
{
    private readonly IGitHubClient _client;
    private readonly string _owner;
    private readonly string _repo;
    private readonly ILogger _logger;

    public GitHubLinkedWork(IGitHubClient client, GitHubTicketConnection connection, ILogger logger)
    {
        _client = client;
        (_owner, _repo) = ParseRepoUrl(connection.RepoUrl);
        _logger = logger;
    }

    public async Task<TicketLinkedWorkResult> ForAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticketId);
        if (!int.TryParse(ticketId.Value, out var number))
            return TicketLinkedWorkResult.Refused($"'{ticketId.Value}' is not a GitHub issue number.");
        try
        {
            var events = await _client.Issue.Timeline.GetAllForIssue(_owner, _repo, number);
            return TicketLinkedWorkResult.Of(events
                .Where(held => held.Event.StringValue is "cross-referenced" or "connected")
                .Where(held => held.Source?.Issue?.PullRequest is not null)
                .Select(held => new TicketLinkedWork(
                    "pull request",
                    $"#{held.Source!.Issue.Number} {held.Source.Issue.Title}",
                    held.Source.Issue.State.StringValue,
                    held.Source.Issue.HtmlUrl)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex,
                "GitHub could not be asked what it links to issue {Ticket}", ticketId.Value);
            return TicketLinkedWorkResult.Refused(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    private static (string owner, string repo) ParseRepoUrl(string url)
    {
        var segments = new Uri(url).AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2) throw new ConfigurationException($"Invalid GitHub URL: {url}");
        return (segments[0], segments[1]);
    }
}
