using System.Text.Json;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: one page of a GitHub repository's open issues. The issues endpoint returns
/// pull requests too; they are not tickets and are not counted.
/// </summary>
public sealed class GitHubTicketCount(IDraftCheckHttp http) : ITicketCountCapability
{
    public TrackerType Type => TrackerType.GitHub;

    public async Task<DraftCheckStep> CountOpenAsync(
        TrackerConnection tracker, string token, CancellationToken cancellationToken)
    {
        var url = $"{GitHubTrackerDraftCheck.Api}/repos/{GitHubTrackerDraftCheck.RepositoryOf(tracker)}"
                  + $"/issues?state=open&per_page={DraftCheckJson.PageSize}";
        var answer = await http.GetAsync(url, DraftCheckAuth.GitHub(token), cancellationToken);
        return answer.CountStep(DraftCheckStep.Tickets, "open tickets", Issues);
    }

    private static (int Count, bool More)? Issues(JsonElement page) =>
        page.Page() is { } full
            ? (page.EnumerateArray().Count(i => !i.TryGetProperty("pull_request", out _)), full.More)
            : null;
}
