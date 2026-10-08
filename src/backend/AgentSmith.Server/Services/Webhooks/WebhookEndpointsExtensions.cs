using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Extensions;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Handlers;
using AgentSmith.Server.Services.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// Webhook endpoints feature-set: WebhookSpawnDispatcher (the shared per-match
/// spawn loop + zero-match handler used by all 13 ticket-event handlers) plus
/// each platform's IWebhookHandler for issue / comment / PR-label / PR-comment /
/// pr-event (p0167a) flavours.
/// </summary>
internal static class WebhookEndpointsExtensions
{
    internal static IServiceCollection AddWebhookHandlers(this IServiceCollection services)
    {
        services.AddSingleton<WebhookSpawnDispatcher>();
        // 2026-10-02-5ab2e: last-seen per platform in the database, so a Redis flush keeps it.
        services.AddSingleton<IWebhookDeliveryTracker, AgentSmith.Infrastructure.Persistence.Services.DbWebhookDeliveryTracker>();
        // 2026-09-25-d83b: the PR-label handlers ask this for the review-request word
        // instead of carrying a literal each.
        services.AddSingleton<PrTriggerLabelResolver>();
        services.AddPrCommentAuthorTrust();
        services.AddSingleton<PrCommentCommandAdmission>();
        services.AddSingleton<PrReviewRouteResolver>();
        services.TryAddSingleton<PrRunContextFactory>();
        // 2026-10-08-e8b9b: the rework entry and the router the ticket-comment handlers share —
        // singletons beside the singleton handlers; the scoped chat launcher is reached through a scope.
        services.AddSingleton<ITicketReopener, Services.Lifecycle.TicketReopener>();
        services.AddSingleton<IReworkParkCheck, Services.Rework.ReworkParkCheck>();
        services.AddSingleton<IReworkLaunch, Services.Rework.ReworkLaunch>();
        services.AddSingleton<IReworkEntry, Services.Rework.ReworkEntry>();
        services.AddSingleton<KeywordCommentRouter>();
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
        // p0167a: pr-opened / pr-synchronize -> pr-review. Registered AFTER the
        // label/comment handlers so existing triggers keep first-match precedence.
        services.AddSingleton<IWebhookHandler, GitHubPrEventWebhookHandler>();
        services.AddSingleton<IWebhookHandler, GitLabMrEventWebhookHandler>();
        services.AddSingleton<IWebhookHandler, AzureDevOpsPrEventWebhookHandler>();
        return services;
    }
}
