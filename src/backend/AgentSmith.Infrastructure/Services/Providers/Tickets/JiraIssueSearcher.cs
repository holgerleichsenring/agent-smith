using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: Jira search helper. Builds the JQL, POSTs to the enhanced-search
/// endpoint <c>/rest/api/3/search/jql</c>, and hands the issues array to
/// <see cref="JiraFieldMapper"/>. Project key is optional — when absent the
/// search runs cross-project.
/// <para>
/// The legacy <c>/rest/api/3/search</c> endpoint was removed by Atlassian
/// (CHANGE-2046) and now returns HTTP 410 Gone. The replacement keeps the same
/// request body (<c>jql</c> / <c>fields</c> / <c>maxResults</c>) and the same
/// <c>issues</c> response array, but paginates via an opaque <c>nextPageToken</c>
/// instead of <c>startAt</c>/<c>total</c>. p0283a: we follow <c>nextPageToken</c>
/// up to <see cref="MaxPages"/> pages instead of reading only the first, so a
/// large result set is no longer silently truncated to 100; hitting the cap warns.
/// </para>
/// </summary>
internal sealed class JiraIssueSearcher(
    TicketProviderHttpClient http, JiraFieldMapper mapper,
    JiraTicketConnection connection, ILogger logger)
{
    private static readonly string[] StandardFields =
        ["summary", "description", "status", "labels"];

    private const int PageSize = 100;
    private const int MaxPages = 10;   // safety cap → 1000 tickets per query

    private readonly string _baseUrl = connection.BaseUrl.TrimEnd('/');
    private readonly string? _projectKey = connection.ProjectKey;
    private readonly string _searchPath = connection.ResolvedEndpoints.Search;

    public async Task<IReadOnlyList<Ticket>> SearchAsync(
        string jqlBody, string descriptor, CancellationToken cancellationToken)
    {
        // Wrap the body in parens: a composed body can contain top-level OR, and JQL binds
        // AND tighter than OR, so `project = X AND a OR b` would wrongly drop the project scope.
        var jql = _projectKey is null ? jqlBody : $"project = \"{_projectKey}\" AND ({jqlBody})";
        var project = _projectKey ?? "<all>";
        var url = $"{_baseUrl}{_searchPath}";
        logger.LogInformation("Jira Search: project={Project} {Descriptor}", project, descriptor);
        logger.LogDebug("Jira Search: POST {Url} jql=[{Jql}]", url, jql);
        var tickets = new List<Ticket>();
        string? pageToken = null;
        var page = 0;
        try
        {
            do
            {
                object body = pageToken is null
                    ? new { jql, fields = StandardFields, maxResults = PageSize }
                    : new { jql, fields = StandardFields, maxResults = PageSize, nextPageToken = pageToken };
                using var doc = await http.SendForJsonOrThrowAsync(
                    HttpMethod.Post, url, body, cancellationToken);
                tickets.AddRange(mapper.MapSearchResponse(doc.RootElement));
                pageToken = doc.RootElement.TryGetProperty("nextPageToken", out var tok)
                    ? tok.GetString() : null;
                page++;
            }
            while (!string.IsNullOrEmpty(pageToken) && page < MaxPages);

            if (!string.IsNullOrEmpty(pageToken))
                logger.LogWarning(
                    "Jira Search: hit {MaxPages}-page cap ({Count} tickets) for {Descriptor} — "
                    + "results truncated; narrow trigger_statuses to shrink the candidate set",
                    MaxPages, tickets.Count, descriptor);
            logger.LogInformation("Jira Search: returned {Count} ticket(s)", tickets.Count);
            return tickets;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Jira Search failed for project={Project}", project);
            return tickets;
        }
    }
}
