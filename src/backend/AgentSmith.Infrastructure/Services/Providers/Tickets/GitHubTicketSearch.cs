using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-27-5c1ea: searches GitHub issues by title and body for the dialog's ticket picker.
/// <para>
/// THE SEARCH API, NOT THE ISSUES LIST, because the issues endpoint cannot filter by text at all —
/// the neighbouring helper filters by label and pages through whole repositories. That choice has
/// a cost worth naming: code search is rate limited far below the rest of GitHub's API and answers
/// a secondary limit with its own exception, which arrives here as a refusal rather than as an
/// empty repository.
/// </para>
/// <para>
/// THE CLIENT IS INJECTED AS ITS INTERFACE. The ticket side constructs its Octokit client inside
/// the provider's constructor, so nothing on it can be asserted; taking <see cref="IGitHubClient"/>
/// makes the request this search builds — the qualifiers, the sort, the page size — testable
/// without a network.
/// </para>
/// </summary>
public sealed class GitHubTicketSearch : ITicketSearch
{
    private readonly IGitHubClient _client;
    private readonly string _owner;
    private readonly string _repo;
    private readonly ILogger _logger;

    public GitHubTicketSearch(IGitHubClient client, GitHubTicketConnection connection, ILogger logger)
    {
        _client = client;
        (_owner, _repo) = ParseRepoUrl(connection.RepoUrl);
        _logger = logger;
    }

    public async Task<TicketSearchResult> SearchAsync(
        string text, int limit, CancellationToken cancellationToken)
    {
        if (Term(text) is not { } term) return TicketSearchResult.None;
        var request = new SearchIssuesRequest(term)
        {
            State = ItemState.Open,
            Type = IssueTypeQualifier.Issue,   // the search endpoint returns pull requests too
            In = [IssueInQualifier.Title, IssueInQualifier.Body],
            SortField = IssueSearchSort.Updated,
            Order = SortDirection.Descending,
            PerPage = limit,
        };
        request.Repos.Add(_owner, _repo);
        try
        {
            _logger.LogDebug("GitHub ticket search: {Owner}/{Repo} term=[{Term}] perPage={Max}",
                _owner, _repo, term, limit);
            var found = await _client.Search.SearchIssues(request);
            var hits = (found.Items ?? [])
                .Select(i => new TicketSearchHit(new TicketId(i.Number.ToString()), i.Title));
            // GitHub reports the whole match count, so the cap is measured rather than inferred.
            return TicketSearchResult.Of(hits, limit, found.TotalCount > limit);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "GitHub ticket search failed for {Owner}/{Repo}", _owner, _repo);
            return TicketSearchResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    /// <summary>
    /// The typed text as a search term, or null when nothing searchable survives. A colon would
    /// make the rest of the word a qualifier and a quote would open a phrase that never closes —
    /// both turn a typed word into a different query, so they become spaces.
    /// </summary>
    internal static string? Term(string text)
    {
        var words = new string(text.Select(c => c is ':' or '"' ? ' ' : c).ToArray())
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0 ? null : string.Join(' ', words);
    }

    private static (string owner, string repo) ParseRepoUrl(string url)
    {
        var segments = new Uri(url).AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2) throw new ConfigurationException($"Invalid GitHub URL: {url}");
        return (segments[0], segments[1]);
    }
}
