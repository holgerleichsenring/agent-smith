using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Extensions;

internal static partial class ServiceCollectionExtensions
{
    // Conversational intent engine + per-intent handlers. AddIntentEngine wires the
    // ILlmIntentParser to the Claude IChatClient and exposes ProjectResolver for the
    // Slack/Teams modal flows; AddIntentHandlers registers the FixTicket/ListTickets/
    // CreateTicket/InitProject/Help intents plus the Slack-specific dispatch / interaction
    // / modal-submission handlers.
    private static IServiceCollection AddIntentEngine(this IServiceCollection services)
    {
        services.AddSingleton<ILlmIntentParser>(sp =>
        {
            var factory = sp.GetRequiredService<IChatClientFactory>();
            return new LlmIntentParser(
                factory,
                new AgentConfig { Type = "claude" },
                sp.GetRequiredService<ILogger<LlmIntentParser>>());
        });
        services.AddSingleton<IProjectResolver, ProjectResolver>();
        services.AddScoped<IntentEngine>();
        return services;
    }

    internal static IServiceCollection AddIntentHandlers(this IServiceCollection services)
    {
        services.AddScoped<FixTicketIntentHandler>();
        services.AddScoped<ListTicketsIntentHandler>();
        services.AddScoped<CreateTicketIntentHandler>();
        services.AddScoped<InitProjectIntentHandler>();
        services.AddScoped<HelpHandler>();
        services.AddScoped<SlackMessageDispatcher>();
        services.AddScoped<SlackErrorActionHandler>();
        services.AddScoped<SlackInteractionHandler>();
        services.AddScoped<SlackModalSubmissionHandler>();
        services.AddSingleton<CachedTicketSearch>();
        services.AddMemoryCache();
        return services;
    }
}
