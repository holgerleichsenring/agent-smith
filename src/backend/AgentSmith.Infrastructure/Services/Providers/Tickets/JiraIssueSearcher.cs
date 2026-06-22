using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: Jira search helper. Builds the JQL, POSTs to /rest/api/3/search,
/// and hands the issues array to <see cref="JiraFieldMapper"/>. Project key
/// is optional — when absent the search runs cross-project.
/// </summary>
internal sealed class JiraIssueSearcher(
    TicketProviderHttpClient http, JiraFieldMapper mapper,
    JiraTicketConnection connection, ILogger logger)
{
    private static readonly string[] StandardFields =
        ["summary", "description", "status", "labels"];

    private readonly string _baseUrl = connection.BaseUrl.TrimEnd('/');
    private readonly string? _projectKey = connection.ProjectKey;

    public async Task<IReadOnlyList<Ticket>> SearchAsync(
        string jqlBody, string descriptor, CancellationToken cancellationToken)
    {
        var jql = _projectKey is null ? jqlBody : $"project = \"{_projectKey}\" AND {jqlBody}";
        var project = _projectKey ?? "<all>";
        logger.LogInformation("Jira Search: project={Project} {Descriptor}", project, descriptor);
        try
        {
            using var doc = await http.SendForJsonOrThrowAsync(
                HttpMethod.Post, $"{_baseUrl}/rest/api/3/search",
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
