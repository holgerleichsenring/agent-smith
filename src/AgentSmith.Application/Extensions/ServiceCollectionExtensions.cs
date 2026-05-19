using AgentSmith.Application.Prompts;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application;

/// <summary>
/// Registers all application services (commands, handlers, pipeline, use cases) with the DI container.
/// Helpers are split into partial files by subdomain (SkillRuntime, PipelineHandlers,
/// TriageServices, SecurityHandlers, BootstrapHandlers, ContextBuilders, PipelineRuntime)
/// so each file stays under the 120-line limit. The public entry point and the shared
/// AddBuilder helper live here.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentSmithCommands(this IServiceCollection services)
    {
        // Transient (not Singleton): a Singleton CommandExecutor would receive the
        // ROOT IServiceProvider regardless of where it's resolved from, so its
        // GetService<ICommandHandler<T>>() calls would resolve handlers from the
        // root scope. Handlers like GeneratePlanHandler depend on Scoped services
        // (IAgentProviderFactory), so this trips ValidateScopes at runtime. Transient
        // means CommandExecutor injects the same provider its caller was resolved
        // from — Server flows resolve through a request scope, CLI flows resolve
        // from root and have no Scoped deps that matter (no DialogueTrail).
        services.AddTransient<ICommandExecutor, CommandExecutor>();
        services.AddSingleton<IPromptOverrideSource, EnvDirectoryPromptOverrideSource>();
        services.AddSingleton<IPromptCatalog, EmbeddedPromptCatalog>();
        services.AddSingleton<AgentSmithConfigValidator>();
        services.AddSingleton<PollingConfigDeprecationWarner>();
        AddPipelineHandlers(services);
        AddTriageServices(services);
        AddSecurityHandlers(services);
        AddBootstrapHandlers(services);
        AddSkillRuntime(services);
        AddContextBuilders(services);
        AddPipelineRuntime(services);
        return services;
    }

    private static void AddBuilder<TBuilder>(IServiceCollection services, string commandName)
        where TBuilder : IContextBuilder, new()
        => services.AddSingleton(new KeyedContextBuilder(commandName, new TBuilder()));
}
