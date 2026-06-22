using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// p0147f: GitLab list-by-labels helper. GitLab's REST <c>labels=</c>
/// parameter ANDs the list (issue must carry ALL labels); for OR-semantics
/// we issue one request per label and dedupe by iid in-memory.
/// </summary>
internal sealed class GitLabIssueLister(
    TicketProviderHttpClient http, GitLabFieldMapper mapper,
    GitLabTicketConnection connection, ILogger logger)
{
    private readonly string _baseUrl = connection.BaseUrl.TrimEnd('/');
    private readonly string _projectPath = connection.ProjectPath;

    public async Task<IReadOnlyList<Ticket>> SearchAsync(
        IReadOnlyCollection<string> labels, string descriptor, CancellationToken cancellationToken)
    {
        logger.LogInformation("GitLab List: project={Project} {Descriptor}", _projectPath, descriptor);
        var deduped = new Dictionary<string, Ticket>();
        foreach (var rawLabel in labels)
        {
            var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/issues"
                + $"?labels={Uri.EscapeDataString(rawLabel)}&state=opened&per_page=100";
            try
            {
                using var doc = await http.SendForJsonOrThrowAsync(HttpMethod.Get, url, null, cancellationToken);
                foreach (var t in mapper.MapMany(doc.RootElement)) deduped[t.Id.Value] = t;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "GitLab List failed for project={Project} label={Label}",
                    _projectPath, rawLabel);
            }
        }
        logger.LogInformation("GitLab List: returned {Count} ticket(s)", deduped.Count);
        return [.. deduped.Values];
    }
}
