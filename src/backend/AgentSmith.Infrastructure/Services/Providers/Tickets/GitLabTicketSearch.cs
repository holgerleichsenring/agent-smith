using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-27-5c1ea: searches GitLab issues by title and description for the dialog's ticket picker.
/// <para>
/// GitLab HAS NO SEARCH SHAPE HERE YET. The neighbouring helper's <c>SearchAsync</c> takes LABELS
/// and fans out one request per label; the issues endpoint's own <c>search=</c> parameter — which
/// defaults to title and description, the two fields this port reveals — had no caller until now.
/// </para>
/// <para>
/// The typed text needs no escaping beyond the URL encoding every parameter on this endpoint
/// already gets: GitLab takes the term as a term, not as a query language.
/// </para>
/// </summary>
public sealed class GitLabTicketSearch(
    GitLabTicketConnection connection, HttpClient httpClient, GitLabFieldMapper mapper, ILogger logger)
    : ITicketSearch
{
    private readonly TicketProviderHttpClient _http =
        TicketProviderHttpClient.WithPrivateToken(httpClient, connection.PrivateToken);

    public async Task<TicketSearchResult> SearchAsync(
        string text, int limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text)) return TicketSearchResult.None;
        // in=title,description is GitLab's default for `search`; it is stated so that a later
        // default change cannot quietly widen what this port matches.
        var url = $"{connection.BaseUrl.TrimEnd('/')}/api/v4/projects/{connection.ProjectPath}/issues"
            + $"?search={Uri.EscapeDataString(text.Trim())}&in=title,description"
            + $"&state=opened&order_by=updated_at&sort=desc&per_page={limit + 1}";
        // 2026-09-28-1da5a: a typed NUMBER also names iids. GitLab has no prefix filter, but it
        // takes an enumerated list — the same mechanism Azure DevOps expresses as ranges — so the
        // prefix is asked for separately and merged, its own query rather than a widened one.
        var byNumber = TicketNumberPrefix.Of(text) is { } prefix
            ? $"{connection.BaseUrl.TrimEnd('/')}/api/v4/projects/{connection.ProjectPath}/issues"
                + $"?state=opened&per_page={limit + 1}&"
                + string.Join("&", TicketNumberPrefix.Ids(prefix).Select(id => $"iids[]={id}"))
            : null;
        try
        {
            logger.LogDebug("GitLab ticket search: GET {Url}", url);
            using var doc = await _http.SendForJsonOrThrowAsync(
                HttpMethod.Get, url, null, cancellationToken);
            var hits = mapper.MapMany(doc.RootElement).ToList();
            if (byNumber is not null)
            {
                using var numbered = await _http.SendForJsonOrThrowAsync(
                    HttpMethod.Get, byNumber, null, cancellationToken);
                hits.AddRange(mapper.MapMany(numbered.RootElement)
                    .Where(n => hits.All(h => h.Id.Value != n.Id.Value)));
            }

            return TicketSearchResult.Of(
                hits.Select(t => new TicketSearchHit(t.Id, t.Title, t.Kind)), limit);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "GitLab ticket search failed for project={Project}", connection.ProjectPath);
            return TicketSearchResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }
}
