using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-27-5c1ea: searches Jira issues by title and body for the dialog's ticket picker.
/// <para>
/// SUMMARY AND DESCRIPTION, NOT <c>text</c>. Jira's <c>text</c> super-field also spans comments,
/// environment and every custom text field, while what the caller is shown is a title — so a hit
/// could contain none of the typed words anywhere a person can see. Two explicit fields keep the
/// match visible.
/// </para>
/// <para>
/// THE OPERAND IS BUILT, NOT INTERPOLATED. This is the first caller to put OPERATOR-TYPED text
/// into a JQL string: every other query in this tree is composed from configuration. A typed
/// quote, backslash or Lucene operator would otherwise leave the tracker with a 400, which the
/// existing search helper turns into an empty list and a person reads as "no such ticket".
/// A trailing wildcard makes it a prefix match, because <c>~</c> is word-based and a picker is
/// typed one character at a time — "auth" has to find "authentication".
/// </para>
/// </summary>
public sealed class JiraTicketSearch(
    JiraTicketConnection connection, HttpClient httpClient, JiraFieldMapper mapper, ILogger logger)
    : ITicketSearch
{
    // Lucene's operators, which Jira passes straight through on a `~` comparison.
    private const string Reserved = "+-&|!(){}[]^\"~*?:\\/";

    private readonly TicketProviderHttpClient _http =
        TicketProviderHttpClient.WithBasicAuth(httpClient, connection.Email, connection.ApiToken);

    public async Task<TicketSearchResult> SearchAsync(
        string text, int limit, CancellationToken cancellationToken)
    {
        if (Operand(text) is not { } operand) return TicketSearchResult.None;
        var match = $"(summary ~ {operand} OR description ~ {operand}) AND statusCategory != Done";
        // The project key is optional by configuration; with none the search runs site-wide,
        // exactly as the by-id fetch on this connection already does.
        var scoped = connection.ProjectKey is { } key ? $"project = \"{key}\" AND ({match})" : match;
        var jql = $"{scoped} ORDER BY updated DESC";
        var url = $"{connection.BaseUrl.TrimEnd('/')}{connection.ResolvedEndpoints.Search}";
        try
        {
            logger.LogDebug("Jira ticket search: jql=[{Jql}] maxResults={Max}", jql, limit + 1);
            using var doc = await _http.SendForJsonOrThrowAsync(
                HttpMethod.Post, url,
                new { jql, fields = new[] { "summary" }, maxResults = limit + 1 },
                cancellationToken);
            var hits = mapper.MapSearchResponse(doc.RootElement)
                .Select(t => new TicketSearchHit(t.Id, t.Title));
            return TicketSearchResult.Of(hits, limit);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Jira ticket search failed — jql=[{Jql}]", jql);
            return TicketSearchResult.Failed(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    /// <summary>
    /// The typed text as a quoted JQL operand with a trailing wildcard, or null when nothing
    /// searchable survives. Reserved characters become spaces rather than being escaped: an
    /// escaped Lucene operator is still an operator to Jira, and a picker's text is words.
    /// </summary>
    internal static string? Operand(string text)
    {
        var words = new string(text.Select(c => Reserved.Contains(c) ? ' ' : c).ToArray())
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0 ? null : $"\"{string.Join(' ', words)}*\"";
    }
}
