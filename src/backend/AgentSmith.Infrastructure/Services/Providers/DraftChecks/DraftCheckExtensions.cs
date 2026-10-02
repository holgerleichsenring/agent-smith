using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Services.Providers.DraftChecks;

/// <summary>
/// 2026-10-02-5f89b: the studio's draft checks on their own named HTTP client, auto-redirect off.
/// </summary>
public static class DraftCheckExtensions
{
    public static IServiceCollection AddDraftChecks(this IServiceCollection services)
    {
        services.AddHttpClient(DraftCheckHttp.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddSingleton<IDraftCheckHttp, DraftCheckHttp>();
        services.AddSingleton<DraftCheckCollector>();
        services.AddSingleton<AzureDevOpsDraftSteps>();
        services.AddSingleton<IConnectionDraftCheck, GitLabConnectionDraftCheck>();
        services.AddSingleton<IConnectionDraftCheck, GitHubConnectionDraftCheck>();
        services.AddSingleton<IConnectionDraftCheck, AzureDevOpsConnectionDraftCheck>();
        services.AddSingleton<ITrackerDraftCheck, GitLabTrackerDraftCheck>();
        services.AddSingleton<ITrackerDraftCheck, GitHubTrackerDraftCheck>();
        services.AddSingleton<ITrackerDraftCheck, AzureDevOpsTrackerDraftCheck>();
        services.AddSingleton<ITrackerDraftCheck, JiraTrackerDraftCheck>();
        services.AddSingleton<ITicketCountCapability, GitLabTicketCount>();
        services.AddSingleton<ITicketCountCapability, GitHubTicketCount>();
        services.AddSingleton<ITicketCountCapability, AzureDevOpsTicketCount>();
        services.AddSingleton<ITicketCountCapability, JiraTicketCount>();
        services.AddSingleton<IConnectionDraftCheckRunner, ConnectionDraftCheckRunner>();
        services.AddSingleton<ITrackerDraftCheckRunner, TrackerDraftCheckRunner>();
        return services;
    }
}
