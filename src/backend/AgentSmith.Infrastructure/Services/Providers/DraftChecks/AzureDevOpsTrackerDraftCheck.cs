using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: an Azure DevOps boards tracker draft — the same identity and project steps as
/// the connection, against the organization URL the provider uses (2026-10-26-cb25b).
/// </summary>
public sealed class AzureDevOpsTrackerDraftCheck(AzureDevOpsDraftSteps shared) : ITrackerDraftCheck
{
    public TrackerType Type => TrackerType.AzureDevOps;

    public static string OrganizationUrl(TrackerConnection tracker) =>
        AzureDevOpsOrganizationUrl.For(tracker.Url, tracker.Organization ?? string.Empty);

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
