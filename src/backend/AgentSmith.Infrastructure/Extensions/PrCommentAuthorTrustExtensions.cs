using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Services.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Infrastructure.Extensions;

/// <summary>
/// The PR-comment author trust, one implementation per code host keyed by platform name,
/// plus the lookups they share. Singletons because the webhook handlers that take them are,
/// and the verdict cache has to outlive a single request to be worth anything.
/// </summary>
public static class PrCommentAuthorTrustExtensions
{
    public static IServiceCollection AddPrCommentAuthorTrust(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IConfiguredRepoFinder, ConfiguredRepoFinder>();
        services.AddSingleton<IPrCommentRepoLookup, PrCommentRepoLookup>();
        services.AddSingleton<IPrCommentTrustVerdictCache, PrCommentTrustVerdictCache>();
        services.AddSingleton<IGitLabMemberAccessReader, GitLabMemberAccessReader>();
        services.AddSingleton<IAzureDevOpsPermissionReader, AzureDevOpsPermissionReader>();
        services.AddKeyedSingleton<IPrCommentAuthorTrust, GitHubAuthorAssociationTrust>("github");
        services.AddKeyedSingleton<IPrCommentAuthorTrust, GitLabMemberAccessTrust>("gitlab");
        services.AddKeyedSingleton<IPrCommentAuthorTrust, AzureDevOpsRepoContributeTrust>("azuredevops");
        return services;
    }
}
