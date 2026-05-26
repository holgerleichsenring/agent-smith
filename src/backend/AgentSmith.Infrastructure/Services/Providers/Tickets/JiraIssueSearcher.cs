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
    string baseUrl, string? projectKey, ILogger logger)
{
    private static readonly string[] StandardFields =
        ["summary", "description", "status", "labels"];

    public async Task<IReadOnlyList<Ticket>> SearchAsync(
        string jqlBody, string descriptor, CancellationToken cancellationToken)
    {
        var jql = projectKey is null ? jqlBody : $"project = \"{projectKey}\" AND {jqlBody}";
        var project = projectKey ?? "<all>";
        logger.LogInformation("Jira Search: project={Project} {Descriptor}", project, descriptor);
        try
        {
            using var doc = await http.TrySendForJsonAsync(
                HttpMethod.Post, $"{baseUrl}/rest/api/3/search",
                new { jql, fields = StandardFields, maxResults = 100 }, cancellationToken);
            if (doc is null)
            {
                logger.LogWarning("Jira Search: non-success for project={Project}", project);
                return [];
            }
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
