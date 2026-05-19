using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Providers.Tickets.OpenQuestions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure;

public static partial class ServiceCollectionExtensions
{
    // Ticket-side factories: provider factory + status-transitioner factory (default,
    // non-locking variant; Server replaces with LockingTicketStatusTransitionerFactory).
    // p0128b: per-platform Plan open-questions comment templates. Keyed by platform name;
    // PlanOpenQuestionsPoster (Application) resolves the matching template.
    private static void AddTicketProviders(IServiceCollection services)
    {
        services.AddSingleton<ITicketProviderFactory, TicketProviderFactory>();
        services.AddSingleton<TicketStatusTransitionerFactory>();
        services.AddSingleton<ITicketStatusTransitionerFactory>(sp =>
            sp.GetRequiredService<TicketStatusTransitionerFactory>());
        services.AddSingleton<Services.Providers.Tickets.JiraWorkflowCatalog>();
        services.AddKeyedSingleton<ITicketCommentTemplate, GitHubOpenQuestionsCommentTemplate>("github");
        services.AddKeyedSingleton<ITicketCommentTemplate, GitLabOpenQuestionsCommentTemplate>("gitlab");
        services.AddKeyedSingleton<ITicketCommentTemplate, AzureDevOpsOpenQuestionsCommentTemplate>("azuredevops");
        services.AddKeyedSingleton<ITicketCommentTemplate, JiraOpenQuestionsCommentTemplate>("jira");
    }
}
