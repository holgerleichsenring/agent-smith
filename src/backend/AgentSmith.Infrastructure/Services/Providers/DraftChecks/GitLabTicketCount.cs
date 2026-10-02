using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>2026-10-02-5f89b: one page of a GitLab project's opened issues.</summary>
public sealed class GitLabTicketCount(IDraftCheckHttp http) : ITicketCountCapability
{
    public TrackerType Type => TrackerType.GitLab;

    public async Task<DraftCheckStep> CountOpenAsync(
        TrackerConnection tracker, string token, CancellationToken cancellationToken)
    {
        var url = $"{GitLabTrackerDraftCheck.ApiOf(tracker)}/projects/{Uri.EscapeDataString(tracker.Project ?? string.Empty)}"
                  + $"/issues?state=opened&per_page={DraftCheckJson.PageSize}";
        var answer = await http.GetAsync(url, DraftCheckAuth.GitLab(token), cancellationToken);
        return answer.CountStep(DraftCheckStep.Tickets, "open tickets", j => j.Page());
    }
}
