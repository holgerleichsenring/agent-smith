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
        try
        {
            logger.LogDebug("GitLab ticket search: GET {Url}", url);
            using var doc = await _http.SendForJsonOrThrowAsync(
                HttpMethod.Get, url, null, cancellationToken);
            var hits = mapper.MapMany(doc.RootElement).Select(t => new TicketSearchHit(t.Id, t.Title));
            return TicketSearchResult.Of(hits, limit);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "GitLab ticket search failed for project={Project}", connection.ProjectPath);
            return TicketSearchResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }
}
