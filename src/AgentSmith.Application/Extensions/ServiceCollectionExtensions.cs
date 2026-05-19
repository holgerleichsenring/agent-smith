using AgentSmith.Application.Prompts;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Application.Services.SkillRounds;
using AgentSmith.Application.Webhooks;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application;

/// <summary>
/// Application composition root: a flat list of per-feature-set Add calls. Removing
/// one call removes one feature-set; the program still compiles. Each feature-set's
/// AddXxx() lives next to the services it registers — find by folder, not by ctrl-F
/// through a monolith.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentSmithCommands(this IServiceCollection services)
    {
        services.AddCommandExecutor();
        services.AddPromptCatalog();
        services.AddConfigurationValidation();
        services.AddPipelineHandlers();
        services.AddSkillRunHandlers();
        services.AddSkillRoundInfrastructure();
        services.AddContextBuilders();
        services.AddPipelineExecution();
        services.AddLoopRuntime();
        services.AddSwaggerCompression();
        services.AddWebhookCommentIntent();
        return services;
    }
}
