using AgentSmith.Application.Models;
using AgentSmith.Application.PipelineDataFlows;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Spawning;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pipeline;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Pipeline;

/// <summary>
/// Pipeline-execution feature-set: executor + its step/error/sandbox
/// collaborators, data-flow resolver + per-preset declarations, sandbox +
/// image resolvers, lifecycle coordinator default, prompt builder, sandbox
/// file-reader factory, project resolver + spawn use-case, and the
/// IntentParser binding.
/// </summary>
public static class PipelineExecutionExtensions
{
    public static IServiceCollection AddPipelineExecution(this IServiceCollection services)
    {
        services.AddTransient<IIntentParser>(sp => new LlmIntentParser(
            sp.GetRequiredService<IChatClientFactory>(),
            sp.GetRequiredService<IConfigurationLoader>(),
            new AgentConfig { Type = "claude" },
            sp.GetRequiredService<IRunContextAccessor>(),
            sp.GetRequiredService<ILogger<LlmIntentParser>>()));
        services.AddTransient<ICommandContextFactory, CommandContextFactory>();
        services.AddTransient<IPipelineStepRunner, PipelineStepRunner>();
        services.AddTransient<IPipelineErrorHandler, PipelineErrorHandler>();
        services.AddTransient<IPipelineSandboxCoordinator, PipelineSandboxCoordinator>();
        services.AddTransient<PipelineExecutor>();
        services.AddTransient<IPipelineExecutor>(sp => sp.GetRequiredService<PipelineExecutor>());
        services.AddSingleton<IPhaseDataFlow, FixBugDataFlow>();
        services.AddSingleton<IPhaseDataFlow, FixNoTestDataFlow>();
        services.AddSingleton<IPhaseDataFlow, AddFeatureDataFlow>();
        services.AddSingleton<IPhaseDataFlow, InitProjectDataFlow>();
        services.AddSingleton<IPhaseDataFlow, SecurityScanDataFlow>();
        services.AddSingleton<IPhaseDataFlow, ApiSecurityScanDataFlow>();
        services.AddSingleton<IPhaseDataFlow, MadDiscussionDataFlow>();
        services.AddSingleton<IPhaseDataFlow, LegalAnalysisDataFlow>();
        services.AddSingleton<IPhaseDataFlow, SkillManagerDataFlow>();
        services.AddSingleton<IPhaseDataFlow, AutonomousDataFlow>();
        services.AddSingleton<IPhaseDataFlowResolver, PhaseDataFlowResolver>();
        services.AddOptions<PipelineDataFlowConfig>().Configure<AgentSmithConfig>(
            (opts, config) => opts.Enforce = config.PipelineDataFlow.Enforce);
        services.AddScoped<DataFlowReadGate>();
        services.AddSingleton<SandboxSpecBuilder>();
        services.AddSingleton<ISandboxResourceResolver, SandboxResourceResolver>();
        services.AddSingleton<IAgentImageResolver, AgentImageResolver>();
        services.AddSingleton<IOrchestratorImageResolver, OrchestratorImageResolver>();
        services.AddTransient<ISandboxLanguageResolver, SandboxLanguageResolver>();
        services.AddTransient<ISourceConfigOverrider, SourceConfigOverrider>();
        services.AddSingleton<IPipelineConfigResolver, PipelineConfigResolver>();
        services.AddSingleton<ProjectResolver>();
        services.AddSingleton<IEnvelopeProjectResolver>(
            sp => sp.GetRequiredService<ProjectResolver>());
        services.AddTransient<ISpawnPipelineRunsUseCase, SpawnPipelineRunsUseCase>();
        services.AddTransient<ExecutePipelineUseCase>();
        services.AddSingleton<IPipelineLifecycleCoordinator, NoOpPipelineLifecycleCoordinator>();
        services.AddSingleton<AgentPromptBuilder>();
        services.AddSingleton<IModelPricingResolver, ModelPricingResolver>();
        services.AddSingleton<ISandboxFileReaderFactory, SandboxFileReaderFactory>();
        services.AddSingleton<IPipelineToolPolicy, AllHostsActivePolicy>();
        services.AddSingleton<IToolKit, ToolKit>();
        return services;
    }
}
