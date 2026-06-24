using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: query-side helper for the GitHub provider. Issues one Octokit
/// request per label (Octokit ANDs the label list, so OR-semantics require
/// fan-out + in-memory dedupe) and maps each <see cref="Issue"/> via the
/// injected <see cref="ITicketFieldMapper{TRaw}"/>.
/// </summary>
internal sealed class GitHubIssueLister
{
    private readonly GitHubClient _client;
    private readonly string _owner;
    private readonly string _repo;
    private readonly ITicketFieldMapper<Issue> _mapper;
    private readonly ILogger _logger;

    public GitHubIssueLister(
        GitHubClient client, GitHubTicketConnection connection,
        ITicketFieldMapper<Issue> mapper, ILogger logger)
    {
        _client = client;
        (_owner, _repo) = ParseGitHubUrl(connection.RepoUrl);
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Ticket>> ListByLabelsAsync(
        IReadOnlyCollection<string> labels, ItemStateFilter state,
        string descriptor, CancellationToken cancellationToken)
    {
        _logger.LogInformation("GitHub List: repo={Owner}/{Repo} {Descriptor}", _owner, _repo, descriptor);
        try
        {
            var deduped = new Dictionary<int, Ticket>();
            foreach (var label in labels)
            {
                var req = new RepositoryIssueRequest { State = state };
                req.Labels.Add(label);
                var issues = await _client.Issue.GetAllForRepository(_owner, _repo, req);
                foreach (var issue in issues)
                    deduped[issue.Number] = _mapper.Map(new TicketId(issue.Number.ToString()), issue);
            }
            _logger.LogInformation("GitHub List: returned {Count} ticket(s)", deduped.Count);
            return [.. deduped.Values];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHub List failed for {Owner}/{Repo} {Descriptor}", _owner, _repo, descriptor);
            return [];
        }
    }

    /// <summary>
    /// Lists all OPEN issues (no label filter) for the poller's discovery pass and
    /// the dashboard/chat ticket listing. Pull requests come back from the issues
    /// endpoint too — they are excluded.
    /// </summary>
    public async Task<IReadOnlyList<Ticket>> ListOpenAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("GitHub List: repo={Owner}/{Repo} open-discovery", _owner, _repo);
        try
        {
            var issues = await _client.Issue.GetAllForRepository(
                _owner, _repo, new RepositoryIssueRequest { State = ItemStateFilter.Open });
            var tickets = issues
                .Where(issue => issue.PullRequest is null)
                .Select(issue => _mapper.Map(new TicketId(issue.Number.ToString()), issue))
                .ToList();
            _logger.LogInformation("GitHub List: returned {Count} ticket(s)", tickets.Count);
            return tickets;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHub List (open) failed for {Owner}/{Repo}", _owner, _repo);
            return [];
        }
    }

    private static (string owner, string repo) ParseGitHubUrl(string url)
    {
        var segments = new Uri(url).AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2) throw new ConfigurationException($"Invalid GitHub URL: {url}");
        return (segments[0], segments[1]);
    }
}
