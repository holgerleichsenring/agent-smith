using AgentSmith.Application.Models;
using AgentSmith.Application.PipelineDataFlows;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pipeline;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application;

public static partial class ServiceCollectionExtensions
{
    // Pipeline orchestration core: executor, intent parsing, sandbox + image resolvers,
    // per-preset IPhaseDataFlow registrations, project + spawn use-cases, and the
    // ToolKit policy. p0128c registers each IPhaseDataFlow as a singleton so the
    // resolver builds an O(1) name→declaration index at startup.
    private static void AddPipelineRuntime(IServiceCollection services)
    {
        services.AddTransient<IIntentParser>(sp => new LlmIntentParser(
            sp.GetRequiredService<IChatClientFactory>(),
            sp.GetRequiredService<IConfigurationLoader>(),
            new AgentConfig { Type = "claude" },
            sp.GetRequiredService<ILogger<LlmIntentParser>>()));
        services.AddTransient<ICommandContextFactory, CommandContextFactory>();
        services.AddTransient<IPipelineExecutor, PipelineExecutor>();
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
        services.AddSingleton<SandboxSpecBuilder>();
        services.AddSingleton<ISandboxResourceResolver, SandboxResourceResolver>();
        services.AddSingleton<IAgentImageResolver, AgentImageResolver>();
        services.AddSingleton<IOrchestratorImageResolver, OrchestratorImageResolver>();
        services.AddTransient<ISandboxLanguageResolver, SandboxLanguageResolver>();
        services.AddTransient<ISourceConfigOverrider, SourceConfigOverrider>();
        services.AddSingleton<IPipelineConfigResolver, PipelineConfigResolver>();
        // p0140a/b: ProjectResolver is stateless; exposed as IEnvelopeProjectResolver for
        // webhook handlers + SpawnPipelineRunsUseCase.
        services.AddSingleton<Services.Triggers.ProjectResolver>();
        services.AddSingleton<IEnvelopeProjectResolver>(
            sp => sp.GetRequiredService<Services.Triggers.ProjectResolver>());
        // p0140b: SpawnPipelineRunsUseCase builds ClaimRequests from a webhook envelope.
        // Depends on ITicketClaimService (Server-only) so this service resolves only inside
        // the Server composition; CLI graph doesn't use it.
        services.AddTransient<ISpawnPipelineRunsUseCase, Services.Spawning.SpawnPipelineRunsUseCase>();
        services.AddTransient<ExecutePipelineUseCase>();
        // ITicketClaimService moved to Server.AddCoreDispatcherServices in p0109a. Application's
        // PipelineExecutor delegates lifecycle wrapping to IPipelineLifecycleCoordinator (NoOp
        // by default; Server overrides).
        services.AddSingleton<IPipelineLifecycleCoordinator, NoOpPipelineLifecycleCoordinator>();
        services.AddSingleton<Services.Prompts.AgentPromptBuilder>();
        services.AddSingleton<ISandboxFileReaderFactory, SandboxFileReaderFactory>();
        // p0145: IToolHost instances are NOT DI-registered — each host carries per-pipeline-run
        // state. ToolKit is stateless (only the policy), so singleton is fine.
        services.AddSingleton<IPipelineToolPolicy, AllHostsActivePolicy>();
        services.AddSingleton<IToolKit, ToolKit>();
    }
}
