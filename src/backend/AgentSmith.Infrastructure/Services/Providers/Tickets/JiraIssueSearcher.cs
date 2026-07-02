using System.Text.Json;
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
        // The exact query the operator can paste into Jira's issue search to reproduce.
        logger.LogDebug("Jira Search: POST {Url} jql=[{Jql}] fields=[{Fields}] maxResults={Max}",
            url, jql, string.Join(",", StandardFields), PageSize);
        var tickets = new List<Ticket>();
        var rawTotal = 0;
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
                var raw = RawIssueCount(doc.RootElement);
                var mapped = mapper.MapSearchResponse(doc.RootElement);
                rawTotal += raw < 0 ? 0 : raw;
                tickets.AddRange(mapped);
                pageToken = doc.RootElement.TryGetProperty("nextPageToken", out var tok)
                    ? tok.GetString() : null;
                // Raw (what Jira matched) vs mapped (what we kept) per page — isolates a Jira
                // 0-match from a mapping loss.
                logger.LogDebug(
                    "Jira Search page {Page}: {Raw} raw issue(s), {Mapped} mapped, hasNextPage={HasNext}",
                    page + 1, raw, mapped.Count, !string.IsNullOrEmpty(pageToken));
                if (raw > mapped.Count)
                    logger.LogWarning(
                        "Jira Search: {Dropped} issue(s) dropped in mapping on page {Page} "
                        + "(missing 'key'/unexpected shape) — jql=[{Jql}]",
                        raw - mapped.Count, page + 1, jql);
                page++;
            }
            while (!string.IsNullOrEmpty(pageToken) && page < MaxPages);

            if (!string.IsNullOrEmpty(pageToken))
                logger.LogWarning(
                    "Jira Search: hit {MaxPages}-page cap ({Count} tickets) for {Descriptor} — "
                    + "results truncated; narrow trigger_statuses to shrink the candidate set",
                    MaxPages, tickets.Count, descriptor);

            // The load-bearing diagnostic for "JQL runs but returns []": if raw=0 Jira itself
            // matched nothing (check label CASE — JQL labels= is case-sensitive — status names,
            // and project key); if raw>0 the mapper dropped them (see the warning above).
            if (tickets.Count == 0)
                logger.LogDebug(
                    "Jira Search: 0 tickets for {Descriptor} — Jira returned {Raw} raw issue(s). "
                    + "jql=[{Jql}]. If raw=0 the query matched nothing (verify label case, status "
                    + "names, project key); if raw>0 the mapper dropped them.",
                    descriptor, rawTotal, jql);
            else
                logger.LogInformation(
                    "Jira Search: returned {Count} ticket(s) (from {Raw} raw)", tickets.Count, rawTotal);
            return tickets;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Jira Search FAILED for project={Project} jql=[{Jql}] — {Message}",
                project, jql, ex.Message);
            return tickets;
        }
    }

    private static int RawIssueCount(JsonElement root)
        => root.TryGetProperty("issues", out var arr) && arr.ValueKind == JsonValueKind.Array
            ? arr.GetArrayLength()
            : -1;   // -1 = no 'issues' array → unexpected response shape
}
