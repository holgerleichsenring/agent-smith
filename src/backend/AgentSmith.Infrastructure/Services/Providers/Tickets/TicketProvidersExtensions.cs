using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Providers.Tickets.OpenQuestions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// Ticket-side factories: provider factory + status-transitioner factory (default,
/// non-locking variant; Server's AddServerCompositionOverrides replaces with the
/// LockingTicketStatusTransitionerFactory). p0128b: per-platform Plan open-questions
/// comment templates keyed by platform name — PlanOpenQuestionsPoster (Application)
/// resolves the matching template via the keyed lookup.
/// </summary>
public static class TicketProvidersExtensions
{
    public static IServiceCollection AddTicketProviders(this IServiceCollection services)
    {
        services.AddSingleton<ITicketProviderFactory, TicketProviderFactory>();
        services.AddSingleton<TicketStatusTransitionerFactory>();
        services.AddSingleton<ITicketStatusTransitionerFactory>(sp =>
            sp.GetRequiredService<TicketStatusTransitionerFactory>());
        services.AddSingleton<JiraWorkflowCatalog>();
        services.AddKeyedSingleton<ITicketCommentTemplate, GitHubOpenQuestionsCommentTemplate>("github");
        services.AddKeyedSingleton<ITicketCommentTemplate, GitLabOpenQuestionsCommentTemplate>("gitlab");
        services.AddKeyedSingleton<ITicketCommentTemplate, AzureDevOpsOpenQuestionsCommentTemplate>("azuredevops");
        services.AddKeyedSingleton<ITicketCommentTemplate, JiraOpenQuestionsCommentTemplate>("jira");
        return services;
    }
}
