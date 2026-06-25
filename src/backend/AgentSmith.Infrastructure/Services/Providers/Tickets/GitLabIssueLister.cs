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
    private const int PerPage = 100;
    private const int MaxPages = 10;   // safety cap → 1000 issues per query

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
                + $"?labels={Uri.EscapeDataString(rawLabel)}&state=opened&per_page={PerPage}";
            try
            {
                foreach (var t in await FetchPagedAsync(url, cancellationToken)) deduped[t.Id.Value] = t;
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

    // p0283a: follow `page` until a short page (or the cap) instead of reading only
    // the first 100 — GitLab issues paginate, so a big project no longer truncates.
    private async Task<IReadOnlyList<Ticket>> FetchPagedAsync(string url, CancellationToken cancellationToken)
    {
        var all = new List<Ticket>();
        for (var page = 1; page <= MaxPages; page++)
        {
            var pageUrl = $"{url}&page={page}";
            logger.LogDebug("GitLab List: GET {Url}", pageUrl);
            using var doc = await http.SendForJsonOrThrowAsync(HttpMethod.Get, pageUrl, null, cancellationToken);
            var batch = mapper.MapMany(doc.RootElement);
            all.AddRange(batch);
            if (batch.Count < PerPage) return all;
        }
        logger.LogWarning(
            "GitLab List: hit {MaxPages}-page cap ({Count} issues) — results truncated; "
            + "narrow trigger_statuses to shrink the candidate set", MaxPages, all.Count);
        return all;
    }

    /// <summary>
    /// Lists all OPEN issues (no label filter) for the poller's discovery pass and
    /// the dashboard/chat ticket listing — the label-fan-out SearchAsync returns
    /// nothing when given no labels.
    /// </summary>
    public async Task<IReadOnlyList<Ticket>> ListOpenAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("GitLab List: project={Project} open-discovery", _projectPath);
        var url = $"{_baseUrl}/api/v4/projects/{_projectPath}/issues?state=opened&per_page={PerPage}";
        try
        {
            var tickets = await FetchPagedAsync(url, cancellationToken);
            logger.LogInformation("GitLab List: returned {Count} ticket(s)", tickets.Count);
            return tickets;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GitLab List (open) failed for project={Project}", _projectPath);
            return [];
        }
    }
}
