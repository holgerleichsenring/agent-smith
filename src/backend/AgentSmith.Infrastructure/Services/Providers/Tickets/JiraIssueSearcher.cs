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
/// instead of <c>startAt</c>/<c>total</c> — we read the first page only, which
/// matches the prior maxResults=100 cap.
/// </para>
/// </summary>
internal sealed class JiraIssueSearcher(
    TicketProviderHttpClient http, JiraFieldMapper mapper,
    JiraTicketConnection connection, ILogger logger)
{
    private static readonly string[] StandardFields =
        ["summary", "description", "status", "labels"];

    private readonly string _baseUrl = connection.BaseUrl.TrimEnd('/');
    private readonly string? _projectKey = connection.ProjectKey;
    private readonly string _searchPath = connection.ResolvedEndpoints.Search;

    public async Task<IReadOnlyList<Ticket>> SearchAsync(
        string jqlBody, string descriptor, CancellationToken cancellationToken)
    {
        var jql = _projectKey is null ? jqlBody : $"project = \"{_projectKey}\" AND {jqlBody}";
        var project = _projectKey ?? "<all>";
        logger.LogInformation("Jira Search: project={Project} {Descriptor}", project, descriptor);
        try
        {
            using var doc = await http.SendForJsonOrThrowAsync(
                HttpMethod.Post, $"{_baseUrl}{_searchPath}",
                new { jql, fields = StandardFields, maxResults = 100 }, cancellationToken);
            var tickets = mapper.MapSearchResponse(doc.RootElement);
            logger.LogInformation("Jira Search: returned {Count} ticket(s)", tickets.Count);
            return tickets;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Jira Search failed for project={Project}", project);
            return [];
        }
    }
}
