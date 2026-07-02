using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Handlers;
using AgentSmith.Server.Services.Webhooks;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// Webhook endpoints feature-set: WebhookSpawnDispatcher (the shared per-match
/// spawn loop + zero-match handler used by all 13 ticket-event handlers) plus
/// each platform's IWebhookHandler for issue / comment / PR-label / PR-comment
/// event flavours.
/// </summary>
internal static class WebhookEndpointsExtensions
{
    internal static IServiceCollection AddWebhookHandlers(this IServiceCollection services)
    {
        services.AddSingleton<WebhookSpawnDispatcher>();
        services.AddSingleton<IWebhookDeliveryTracker, WebhookDeliveryTracker>();
        services.AddSingleton<IWebhookHandler, GitHubIssueWebhookHandler>();
        services.AddSingleton<IWebhookHandler, GitHubIssueCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, GitHubPrLabelWebhookHandler>();
        services.AddSingleton<IWebhookHandler, GitHubPrCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, GitLabIssueWebhookHandler>();
        services.AddSingleton<IWebhookHandler, GitLabIssueCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, GitLabMrLabelWebhookHandler>();
        services.AddSingleton<IWebhookHandler, GitLabMrCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, AzureDevOpsWorkItemWebhookHandler>();
        services.AddSingleton<IWebhookHandler, AzureDevOpsWorkItemCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, AzureDevOpsPrCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, JiraAssigneeWebhookHandler>();
        services.AddSingleton<IWebhookHandler, JiraCommentWebhookHandler>();
        return services;
    }
}
