using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: one page of the enhanced search (the tracker's own search endpoint) for the
/// tickets the provider calls open — statusCategory != Done, in the declared project if any.
/// The search answers no total, so a page that is not the last says "at least".
/// </summary>
public sealed class JiraTicketCount(IDraftCheckHttp http) : ITicketCountCapability
{
    public TrackerType Type => TrackerType.Jira;

    public async Task<DraftCheckStep> CountOpenAsync(
        TrackerConnection tracker, string token, CancellationToken cancellationToken)
    {
        var jql = string.IsNullOrWhiteSpace(tracker.Project)
            ? "statusCategory != Done"
            : $"project = \"{tracker.Project.Replace("\"", "\\\"")}\" AND statusCategory != Done";
        var body = new { jql, maxResults = DraftCheckJson.PageSize, fields = new[] { "id" } };
        var answer = await http.PostJsonAsync(
            $"{JiraTrackerDraftCheck.BaseOf(tracker)}{tracker.Endpoints.Search}", body,
            JiraTrackerDraftCheck.AuthOf(tracker, token), cancellationToken);
        return answer.CountStep(DraftCheckStep.Tickets, "open tickets", Issues);
    }

    private static (int Count, bool More)? Issues(JsonElement page) =>
        page.PageUnder("issues") is { } issues
            ? (issues.Count, page.TryGetProperty("isLast", out var last) ? !last.GetBoolean() : issues.More)
            : null;
}
