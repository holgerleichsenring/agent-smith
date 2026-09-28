using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-28-1da5c: the merge requests GitLab links to an issue.
/// <para>
/// BRANCHES ARE NOT HERE. GitLab links merge requests to an issue and does not link branches to
/// one — a branch is a repository fact, and reading a repository is what this conversation's own
/// source sandboxes already do. Answering "no branches" would be a claim about the board rather
/// than about the question.
/// </para>
/// </summary>
public sealed class GitLabLinkedWork(
    GitLabTicketConnection connection, HttpClient httpClient, ILogger logger) : ITicketLinkedWork
{
    private readonly TicketProviderHttpClient _http =
        TicketProviderHttpClient.WithPrivateToken(httpClient, connection.PrivateToken);

    public async Task<TicketLinkedWorkResult> ForAsync(
        TicketId ticketId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticketId);
        var url = $"{connection.BaseUrl.TrimEnd('/')}/api/v4/projects/{connection.ProjectPath}"
            + $"/issues/{ticketId.Value}/related_merge_requests";
        try
        {
            using var doc = await _http.SendForJsonOrThrowAsync(
                HttpMethod.Get, url, null, cancellationToken);
            return TicketLinkedWorkResult.Of(
                doc.RootElement.EnumerateArray().Select(Merge));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "GitLab could not be asked what it links to issue {Ticket}", ticketId.Value);
            return TicketLinkedWorkResult.Refused(ex.Message.Split('\n', 2)[0].Trim());
        }
    }

    private static TicketLinkedWork Merge(JsonElement request) => new(
        "pull request",
        Text(request, "title") is { Length: > 0 } title
            ? $"!{Text(request, "iid")} {title}"
            : $"!{Text(request, "iid")}",
        Text(request, "state"),
        Text(request, "web_url"));

    private static string Text(JsonElement held, string name) =>
        held.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty
                : value.ToString()
            : string.Empty;
}
