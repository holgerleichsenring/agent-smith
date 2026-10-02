using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: an Azure DevOps boards tracker draft — the same identity and project steps as
/// the connection. The tracker always talks to dev.azure.com/{organization}.
/// </summary>
public sealed class AzureDevOpsTrackerDraftCheck(AzureDevOpsDraftSteps shared) : ITrackerDraftCheck
{
    public TrackerType Type => TrackerType.AzureDevOps;

    public static string OrganizationUrl(TrackerConnection tracker) =>
        $"https://dev.azure.com/{Uri.EscapeDataString(tracker.Organization ?? string.Empty)}";

    public IAsyncEnumerable<DraftCheckStep> RunAsync(
        TrackerConnection tracker, string token, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(tracker.Organization) || string.IsNullOrWhiteSpace(tracker.Project)
            ? Refuse()
            : shared.IdentityAndProjectAsync(OrganizationUrl(tracker), tracker.Project, token, cancellationToken);

    private static async IAsyncEnumerable<DraftCheckStep> Refuse()
    {
        await Task.CompletedTask;
        yield return DraftCheckStep.Fail(DraftCheckStep.Host, "The tracker names no organization or project.");
    }
}
