using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Tickets;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: one WIQL page of the project's open work items, "open" meaning what the
/// discovery lister means (<see cref="AzureDevOpsOpenScope"/>, the tracker's open_states).
/// </summary>
public sealed class AzureDevOpsTicketCount(IDraftCheckHttp http) : ITicketCountCapability
{
    public TrackerType Type => TrackerType.AzureDevOps;

    public async Task<DraftCheckStep> CountOpenAsync(
        TrackerConnection tracker, string token, CancellationToken cancellationToken)
    {
        var project = tracker.Project ?? string.Empty;
        var query = "SELECT [System.Id] FROM WorkItems WHERE "
                    + AzureDevOpsOpenScope.Where(project, tracker.OpenStates.Count > 0 ? tracker.OpenStates : null);
        var url = $"{AzureDevOpsTrackerDraftCheck.OrganizationUrl(tracker)}/{Uri.EscapeDataString(project)}"
                  + $"/_apis/wit/wiql?$top={DraftCheckJson.PageSize}&api-version=7.1";
        var answer = await http.PostJsonAsync(
            url, new { query }, DraftCheckAuth.Basic(string.Empty, token), cancellationToken);
        return answer.CountStep(DraftCheckStep.Tickets, "open tickets", j => j.PageUnder("workItems"));
    }
}
