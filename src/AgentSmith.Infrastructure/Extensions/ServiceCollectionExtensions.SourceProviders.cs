using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Webhooks;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure;

public static partial class ServiceCollectionExtensions
{
    // Source-control providers: GitHub/AzDo client factories + the multi-provider source
    // factory + HostSourceCloner (Application uses for repo checkouts).
    // PR comment reply + conversation lookup (p59, p59b, p59c): keyed by platform name;
    // IConversationLookup → RedisConversationLookup is registered by AgentSmith.Cli/
    // ServiceProviderFactory when REDIS_URL is available (p0101).
    private static void AddSourceProviders(IServiceCollection services)
    {
        services.AddSingleton<Services.Providers.Source.IGitHubClientFactory, Services.Providers.Source.DefaultGitHubClientFactory>();
        services.AddSingleton<Services.Providers.Source.IAzDoClientFactory, Services.Providers.Source.DefaultAzDoClientFactory>();
        services.AddSingleton<ISourceProviderFactory, SourceProviderFactory>();
        services.AddSingleton<IHostSourceCloner, Services.Providers.Source.HostSourceCloner>();
        services.AddSingleton<IPrCommentReplyService, GitHubPrCommentReplyService>();
        services.AddKeyedSingleton<IPrCommentReplyService, GitHubPrCommentReplyService>("github");
        services.AddKeyedSingleton<IPrCommentReplyService, GitLabMrCommentReplyService>("gitlab");
        services.AddKeyedSingleton<IPrCommentReplyService, AzureDevOpsPrCommentReplyService>("azuredevops");
    }
}
