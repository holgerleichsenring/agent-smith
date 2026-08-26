using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Tools;
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
        // p0353: web_fetch tool host as a typed HttpClient (DI, not a per-run `new`).
        services.AddHttpClient<WebToolHost>();
        services.AddSingleton<OutcomeClassifier>();
        services.AddSingleton<NoOpSkillOutputValidator>();
        services.AddSingleton<ISkillOutputValidator>(sp => sp.GetRequiredService<NoOpSkillOutputValidator>());
        services.AddSingleton<RetryCoordinator>();
        services.AddSingleton<RuntimeObservationFactory>();
        // p0423: the bound's report channel (AsyncLocal, so one instance serves every
        // concurrent tool call) and the prompt dump, both services the composition root
        // can see rather than statics the runtime reaches for.
        services.AddSingleton<ResultBoundReporter>();
        // p0423: a run records its conversation only when asked; the null writer is the
        // default so every producer can call it unconditionally and pay nothing.
        services.AddSingleton<Contracts.Runs.TraceSwitch>();
        services.AddSingleton<Contracts.Runs.SecretMasker>();
        services.TryAddSingleton<Contracts.Runs.IRunTraceWriter, Contracts.Runs.NullRunTraceWriter>();
        services.AddSingleton<SkillPromptLogger>();
        services.AddScoped<ISkillCallRuntime, SkillCallRuntime>();

        // p0177: agentic loop core + sub-agent collaborators.
        // SubAgentBudget is scoped per run; one pipeline-execution scope ==
        // one run total. SubAgentNameValidator is stateless and shared.
        services.AddScoped<IAgenticLoopRunner, AgenticLoopRunner>();
        services.AddScoped<ISubAgentRunner, SubAgentRunner>();
        services.AddSingleton<SubAgentNameValidator>();
        // p0280: in-memory child-answer store, scoped per run (one pipeline-execution
        // scope == one run) — the functional child->master detail channel.
        services.AddScoped<Contracts.Services.IChildAnswerStore, InMemoryChildAnswerStore>();
        services.AddScoped(sp =>
        {
            var limits = sp.GetRequiredService<Contracts.Models.Configuration.LoopLimitsConfig>();
            return new SubAgentBudget(limits.MaxSubAgentsPerRun);
        });
        services.AddSingleton<JsonSchemaLoader>();
        // 2026-08-25-c9c7: the gate write_context_yaml judges a context document with.
        services.AddSingleton<ContextSchemaProvider>();
        services.AddSingleton<ContextStackImageRule>();
        // 2026-08-26-167c: the rejection's own parts — pointer into the schema,
        // the grouped report, and the single-value-reads-as-a-list normaliser.
        services.AddSingleton<ContextSchemaPointer>();
        services.AddSingleton<ContextDefectReport>();
        services.AddSingleton<ContextSingleValueNormaliser>();
        services.AddSingleton<ContextSchemaRule>();
        services.AddSingleton<ContextDocumentGate>();
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
