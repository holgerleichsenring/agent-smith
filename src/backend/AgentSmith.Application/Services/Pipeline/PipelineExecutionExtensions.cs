using AgentSmith.Application.Models;
using AgentSmith.Application.PipelineDataFlows;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pipeline;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        // p0201: per-pipeline-run liveness supervisor. Default no-op so InProcess
        // / unit-test compositions stay quiet; Server composition overrides with
        // SandboxLivenessSupervisor in SandboxBackendRegistrations.
        services.AddTransient<ISandboxLivenessSupervisor, NoOpSandboxLivenessSupervisor>();
        services.AddTransient<PipelineExecutor>();
        services.AddTransient<IPipelineExecutor>(sp => sp.GetRequiredService<PipelineExecutor>());
        // p0327: durable dialogue — the hybrid ask gate, checkpoint writer,
        // context (de)serializer, resume reader, and the queue-riding resumer.
        services.AddTransient<IPipelineContextSerializer, Resume.PipelineContextSerializer>();
        services.AddTransient<IDialogueCheckpointWriter, Resume.DialogueCheckpointWriter>();
        services.AddTransient<IDialogueAskGate, Resume.DialogueAskGate>();
        services.AddTransient<Resume.ResumeRequestReader>();
        services.AddTransient<IRunResumer, Resume.RunResumer>();
        // DB-free defaults; the server's relational composition replaces these.
        services.TryAddSingleton<IRunCheckpointStore, Resume.NoOpRunCheckpointStore>();
        services.TryAddSingleton<IDialogueAnswerInbox, Resume.NoOpDialogueAnswerInbox>();
        services.TryAddSingleton<ICapacityQueue, Spawning.NoOpCapacityQueue>();
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
        services.AddSingleton<IPhaseDataFlow, PrReviewDataFlow>();
        services.AddSingleton<IPhaseDataFlow, SpecDialogDataFlow>();
        services.AddSingleton<IPhaseDataFlow, PhaseExecutionDataFlow>();
        services.AddSingleton<IPhaseDataFlowResolver, PhaseDataFlowResolver>();
        services.AddOptions<PipelineDataFlowConfig>().Configure<AgentSmithConfig>(
            (opts, config) => opts.Enforce = config.PipelineDataFlow.Enforce);
        services.AddScoped<DataFlowReadGate>();
        services.AddSingleton<SandboxSpecBuilder>();
        services.AddSingleton<ISandboxResourceResolver, SandboxResourceResolver>();
        // p0269a: default capacity probe — admits everything. The Server composition
        // replaces it with the Kubernetes / Docker probe for the selected backend.
        // TryAdd so a composition that DOES register a real backend probe wins.
        services.TryAddSingleton<ISandboxCapacityProbe, UnboundedCapacityProbe>();
        // p0355: default corpse reaper — nothing to reap (no pod backend). The
        // Kubernetes backend swaps in the real pod sweep. TryAdd so that wins.
        services.TryAddSingleton<ISandboxCorpseReaper, NoOpSandboxCorpseReaper>();
        // p0320b: default orchestrator sizing — null (in-process compositions spawn
        // no orchestrator pod). The Server composition replaces it with the
        // JobSpawnerOptions-backed resolver.
        services.TryAddSingleton<IOrchestratorResourceResolver, NullOrchestratorResourceResolver>();
        services.AddSingleton<IAgentImageResolver, AgentImageResolver>();
        services.AddSingleton<ISandboxSecretsResolver, SandboxSecretsResolver>();
        services.AddSingleton<IOrchestratorImageResolver, OrchestratorImageResolver>();
        // p0270a: the single config resolution pass — owns timeout + cost-cap
        // resolution and composes the resolvers above, so the run path and the
        // dashboard read one materialized resolution.
        services.AddSingleton<Configuration.IConfigResolver, Configuration.ConfigResolutionPass>();
        services.AddTransient<ISandboxLanguageResolver, SandboxLanguageResolver>();
        services.AddTransient<ISourceConfigOverrider, SourceConfigOverrider>();
        services.AddSingleton<IPipelineConfigResolver, PipelineConfigResolver>();
        services.AddSingleton<ProjectResolver>();
        services.AddSingleton<IEnvelopeProjectResolver>(
            sp => sp.GetRequiredService<ProjectResolver>());
        services.AddTransient<Polling.ITrackerDiscoveryQueryBuilder, Polling.TrackerDiscoveryQueryBuilder>();
        // NB: ISpawnPipelineRunsUseCase is NOT registered here — it depends on
        // ITicketClaimService, a Server-only service (webhook + poller fan-out).
        // Registering it in this shared extension made it unconstructable in the
        // CLI graph; it lives in the Server composition (DispatcherExtensions).
        services.AddTransient<ExecutePipelineUseCase>();
        services.AddSingleton<IPipelineLifecycleCoordinator, NoOpPipelineLifecycleCoordinator>();
        // Safe default lease so ExecutePipelineUseCase resolves in EVERY composition
        // root (CLI, tests). A ticket-bearing deployment (Server + persistence) swaps
        // in DbActiveRunLease later; the CLI holds no lease. Symmetric to the no-op
        // lifecycle coordinator above — without this the CLI cannot construct the
        // use case at all (DI throws on IActiveRunLease).
        services.AddSingleton<IActiveRunLease, NoOpActiveRunLease>();
        // p0330: safe default for the pre-start cancel gates — without a
        // relational store there is no persisted flag to read. The Server
        // composition swaps in DbRunCancelStateReader.
        services.AddSingleton<IRunCancelStateReader, NoOpRunCancelStateReader>();
        // p0200: per-run CTS registry powers the cancel endpoint + watchdog.
        services.AddSingleton<IRunCancellationRegistry, RunCancellationRegistry>();
        services.AddSingleton<AgentPromptBuilder>();
        services.AddSingleton<IModelPricingResolver, ModelPricingResolver>();
        services.AddSingleton<ISandboxFileReaderFactory, SandboxFileReaderFactory>();
        services.AddSingleton<IPipelineToolPolicy, AllHostsActivePolicy>();
        services.AddSingleton<IToolKit, ToolKit>();
        return services;
    }
}
