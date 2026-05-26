using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Constructs authenticated Azure DevOps SDK clients. Lifted out of
/// AzureReposSourceProvider so tests can pass stubbed clients — previously each
/// call hard-newed a VssConnection inline with no injection seam.
///
/// Three production call sites (sync git client, async git client for comment
/// threads, work-item-tracking client for PR↔work-item linking) all funnel
/// through this interface so a future telemetry / retry decorator only needs to
/// wrap one type.
/// </summary>
public interface IAzDoClientFactory
{
    GitHttpClient CreateGitClient(string organizationUrl, string personalAccessToken);

    Task<GitHttpClient> CreateGitClientAsync(
        string organizationUrl, string personalAccessToken, CancellationToken cancellationToken);

    WorkItemTrackingHttpClient CreateWorkItemClient(string organizationUrl, string personalAccessToken);
}
