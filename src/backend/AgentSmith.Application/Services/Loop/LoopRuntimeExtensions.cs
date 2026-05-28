using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// Skill-call loop runtime: PipelineConcurrencyGate (scoped — one per pipeline-run
/// DI scope), OutcomeClassifier / RetryCoordinator / NoOpSkillOutputValidator
/// (stateless singletons), RuntimeObservationFactory (p0147b — stateless factory
/// that maps Incomplete/FailedRuntime outcomes into typed observations so silent
/// skill drops become pipeline-visible), the scoped SkillCallRuntime that composes
/// the collaborators, the schema validators + factory (JsonSchemaLoader caches the
/// four hand-written schemas at boot for the process lifetime — singleton), and the
/// in-memory run-artifact-store fallback (Cli/Server replace with RedisRunArtifactStore
/// when a ConnectionMultiplexer is available).
/// </summary>
public static class LoopRuntimeExtensions
{
    public static IServiceCollection AddLoopRuntime(this IServiceCollection services)
    {
        services.AddScoped<PipelineConcurrencyGate>();
        services.AddSingleton<OutcomeClassifier>();
        services.AddSingleton<NoOpSkillOutputValidator>();
        services.AddSingleton<ISkillOutputValidator>(sp => sp.GetRequiredService<NoOpSkillOutputValidator>());
        services.AddSingleton<RetryCoordinator>();
        services.AddSingleton<RuntimeObservationFactory>();
        services.AddScoped<ISkillCallRuntime, SkillCallRuntime>();

        // p0177: agentic loop core + sub-agent collaborators.
        // SubAgentBudget is scoped per run; one pipeline-execution scope ==
        // one run total. SubAgentNameValidator is stateless and shared.
        services.AddScoped<IAgenticLoopRunner, AgenticLoopRunner>();
        services.AddScoped<ISubAgentRunner, SubAgentRunner>();
        services.AddSingleton<SubAgentNameValidator>();
        services.AddScoped(sp =>
        {
            var limits = sp.GetRequiredService<Contracts.Models.Configuration.LoopLimitsConfig>();
            return new SubAgentBudget(limits.MaxSubAgentsPerRun);
        });
        services.AddSingleton<JsonSchemaLoader>();
        services.AddSingleton<PlanOutputValidator>();
        services.AddSingleton<DiffOutputValidator>();
        services.AddSingleton<BootstrapOutputValidator>();
        services.AddTransient<ObservationOutputValidator>();
        services.AddSingleton<DiscoveryOutputValidator>();
        services.AddSingleton<SkillOutputValidatorFactory>();
        services.TryAddSingleton<IRunArtifactStore>(_ => new InMemoryRunArtifactStore());
        return services;
    }
}
