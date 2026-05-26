using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// Production AzDo client factory: VssBasicCredential with empty username + PAT.
/// Stateless — registered as a DI singleton.
/// </summary>
public sealed class DefaultAzDoClientFactory : IAzDoClientFactory
{
    public GitHttpClient CreateGitClient(string organizationUrl, string personalAccessToken)
    {
        return Connect(organizationUrl, personalAccessToken).GetClient<GitHttpClient>();
    }

    public Task<GitHttpClient> CreateGitClientAsync(
        string organizationUrl, string personalAccessToken, CancellationToken cancellationToken)
    {
        return Connect(organizationUrl, personalAccessToken).GetClientAsync<GitHttpClient>(cancellationToken);
    }

    public WorkItemTrackingHttpClient CreateWorkItemClient(string organizationUrl, string personalAccessToken)
    {
        return Connect(organizationUrl, personalAccessToken).GetClient<WorkItemTrackingHttpClient>();
    }

    private static VssConnection Connect(string organizationUrl, string personalAccessToken)
    {
        var creds = new VssBasicCredential(string.Empty, personalAccessToken);
        return new VssConnection(new Uri(organizationUrl), creds);
    }
}
