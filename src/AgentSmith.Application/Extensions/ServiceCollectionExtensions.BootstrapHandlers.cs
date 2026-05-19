using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application;

public static partial class ServiceCollectionExtensions
{
    // p0125c/d: typed concept publication. Handlers are registered three times:
    //   1. concrete type (transient) — resolvable for IConceptWriter dual-registration
    //   2. ICommandHandler<TContext> — pipeline execution path
    //   3. IConceptWriter (singleton-of-handler) — build-time validate-concepts registry
    // p0130a: BootstrapGate is a policy handler — reads concepts published by
    // BootstrapCheckHandler and aborts the pipeline when bootstrap files are missing.
    // p0130c: PublishProjectLanguage publishes the project_language enum.
    // BootstrapDispatch deterministic SkillRound emit for init-project.
    // p0130c-followup: producer-loop runtime for bootstrap skills (csharp/node/
    // python/generic-bootstrap). Distinct from SkillRound because the chat call
    // carries WriteFile + the bootstrap PathWriteGuard.
    private static void AddBootstrapHandlers(IServiceCollection services)
    {
        services.AddTransient<PipelineNameInitializerHandler>();
        services.AddTransient<ICommandHandler<PipelineNameInitializerContext>>(sp =>
            sp.GetRequiredService<PipelineNameInitializerHandler>());
        services.AddSingleton<IConceptWriter>(sp =>
            sp.GetRequiredService<PipelineNameInitializerHandler>());

        services.AddTransient<BootstrapCheckHandler>();
        services.AddTransient<ICommandHandler<BootstrapCheckContext>>(sp =>
            sp.GetRequiredService<BootstrapCheckHandler>());
        services.AddSingleton<IConceptWriter>(sp =>
            sp.GetRequiredService<BootstrapCheckHandler>());

        services.AddTransient<ICommandHandler<BootstrapGateContext>, BootstrapGateHandler>();

        services.AddTransient<PublishProjectLanguageHandler>();
        services.AddTransient<ICommandHandler<PublishProjectLanguageContext>>(sp =>
            sp.GetRequiredService<PublishProjectLanguageHandler>());
        services.AddSingleton<IConceptWriter>(sp =>
            sp.GetRequiredService<PublishProjectLanguageHandler>());

        services.AddTransient<ICommandHandler<BootstrapDispatchContext>, BootstrapDispatchHandler>();
        services.AddTransient<ICommandHandler<BootstrapRoundContext>, BootstrapRoundHandler>();

        services.AddSingleton<ConceptWriterRegistry>();
    }
}
