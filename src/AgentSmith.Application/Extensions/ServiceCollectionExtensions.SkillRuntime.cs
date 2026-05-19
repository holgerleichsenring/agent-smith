using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application;

public static partial class ServiceCollectionExtensions
{
    // p0126b: skill-call collaborator services. PipelineConcurrencyGate is scoped
    // (one per pipeline-run DI scope); OutcomeClassifier, RetryCoordinator and the
    // default NoOpSkillOutputValidator are stateless singletons.
    // p0126c: SkillCallRuntime is scoped (one per pipeline run); composes the five
    // collaborator services into the public ExecuteAsync flow.
    // p0128a: schema validators + factory. JsonSchemaLoader caches all four
    // hand-written schemas at boot for the process lifetime. In-memory artifact store
    // is the safe default; AgentSmith.Cli/Server's Redis-gated registration replaces
    // it with RedisRunArtifactStore when a ConnectionMultiplexer is available.
    private static void AddSkillRuntime(IServiceCollection services)
    {
        services.AddScoped<PipelineConcurrencyGate>();
        services.AddSingleton<OutcomeClassifier>();
        services.AddSingleton<NoOpSkillOutputValidator>();
        services.AddSingleton<ISkillOutputValidator>(sp => sp.GetRequiredService<NoOpSkillOutputValidator>());
        services.AddSingleton<RetryCoordinator>();
        services.AddScoped<ISkillCallRuntime, SkillCallRuntime>();
        services.AddSingleton<JsonSchemaLoader>();
        services.AddSingleton<PlanOutputValidator>();
        services.AddSingleton<DiffOutputValidator>();
        services.AddSingleton<BootstrapOutputValidator>();
        services.AddSingleton<ObservationOutputValidator>();
        services.AddSingleton<SkillOutputValidatorFactory>();
        services.TryAddSingleton<IRunArtifactStore>(_ => new InMemoryRunArtifactStore());
    }
}
