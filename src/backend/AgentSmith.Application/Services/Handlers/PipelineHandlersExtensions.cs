using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Per-command handlers driven by PipelineExecutor: ticket fetch, source checkout,
/// load context / coding-principles / skills / swagger, analyze, plan-related, test,
/// commit, init-commit, generate-docs/tests, compile-discussion/knowledge,
/// acquire-source, deliver-output/findings, session-setup, ask, triage + activation,
/// bootstrap (check/gate/dispatch + concept publishers), SpawnX security launchers,
/// pattern/git-history/dependency scanners + their findings compactors.
/// Triple-registered handlers (CheckoutSource, TryCheckoutSource, PublishProjectLanguage,
/// PipelineNameInitializer, BootstrapCheck) expose IConceptWriter to the registry.
/// </summary>
public static class PipelineHandlersExtensions
{
    public static IServiceCollection AddPipelineHandlers(this IServiceCollection services)
    {
        services.AddTransient<ICommandHandler<LoadCatalogContext>, LoadCatalogHandler>();
        services.AddTransient<ICommandHandler<FetchTicketContext>, FetchTicketHandler>();
        AddConceptPublishingHandler<CheckoutSourceHandler, CheckoutSourceContext>(services);
        AddConceptPublishingHandler<TryCheckoutSourceHandler, TryCheckoutSourceContext>(services);
        services.AddTransient<ICommandHandler<SetupRegistryAuthContext>, SetupRegistryAuthHandler>();
        services.AddTransient<ICommandHandler<EnsurePrerequisitesContext>, EnsurePrerequisitesHandler>();
        services.AddTransient<ICommandHandler<LoadCodingPrinciplesContext>, LoadCodingPrinciplesHandler>();
        services.AddTransient<ICommandHandler<AnalyzeCodeContext>, AnalyzeProjectHandler>();
        services.AddTransient<IProjectMapJsonReader, ProjectMapJsonReader>();
        services.AddTransient<IProjectAnalyzer, ProjectAnalyzer>();
        services.AddTransient<ICommandHandler<AnalyzePrDiffContext>, AnalyzePrDiffHandler>();
        services.AddTransient<IUnifiedDiffParser, UnifiedDiffParser>();
        services.AddTransient<ICommandHandler<EmptyPlanCheckContext>, EmptyPlanCheckHandler>();
        services.AddTransient<ICommandHandler<ApprovalContext>, ApprovalHandler>();
        services.AddTransient<ICommandHandler<AgenticExecuteContext>, AgenticExecuteHandler>();
        services.AddTransient<ICommandHandler<AgenticMasterContext>, AgenticMasterHandler>();
        services.AddTransient<SandboxGitOperations>();
        services.AddSingleton<ISecretPatternScanner, SecretPatternScanner>();
        services.AddTransient<ICommandHandler<CommitAndPRContext>, CommitAndPRHandler>();
        services.AddTransient<ICommandHandler<LoadContextContext>, LoadContextHandler>();
        services.AddTransient<ICommandHandler<WriteRunResultContext>, WriteRunResultHandler>();
        services.AddTransient<ICommandHandler<InitCommitContext>, InitCommitHandler>();
        services.AddTransient<ICommandHandler<PrCrossLinkContext>, PrCrossLinkHandler>();
        services.AddTransient<ICommandHandler<SwitchSkillContext>, SwitchSkillHandler>();
        services.AddTransient<ICommandHandler<PhaseAdvanceContext>, PhaseAdvanceHandler>();
        services.AddTransient<ICommandHandler<PersistWorkBranchContext>, PersistWorkBranchHandler>();
        services.AddTransient<ICommandHandler<GenerateTestsContext>, GenerateTestsHandler>();
        services.AddTransient<ICommandHandler<GenerateDocsContext>, GenerateDocsHandler>();
        services.AddTransient<ICommandHandler<CompileDiscussionContext>, CompileDiscussionHandler>();
        services.AddTransient<ICommandHandler<AcquireSourceContext>, AcquireSourceHandler>();
        services.AddTransient<ICommandHandler<BootstrapDocumentContext>, BootstrapDocumentHandler>();
        services.AddTransient<ICommandHandler<DeliverOutputContext>, DeliverOutputHandler>();
        services.AddTransient<ICommandHandler<SessionSetupContext>, SessionSetupHandler>();
        services.AddTransient<ICommandHandler<AskContext>, AskCommandHandler>();
        services.AddTransient<ICommandHandler<CompileKnowledgeContext>, CompileKnowledgeHandler>();
        services.AddTransient<ICommandHandler<QueryKnowledgeContext>, QueryKnowledgeHandler>();
        services.AddTransient<ICommandHandler<LoadRunsContext>, LoadRunsHandler>();
        services.AddTransient<ICommandHandler<WriteTicketsContext>, WriteTicketsHandler>();
        services.AddSingleton<KnowledgePromptBuilder>();
        services.AddSingleton<StructuredOutputInstructionBuilder>();
        services.AddTransient<PromptPrefixBuilder>();
        services.AddTransient<ISkillPromptBuilder, SkillPromptBuilder>();
        services.AddTransient<IGateOutputHandler, GateOutputHandler>();
        services.AddTransient<IGateRetryCoordinator, GateRetryCoordinator>();
        services.AddTransient<IUpstreamContextBuilder, UpstreamContextBuilder>();
        services.AddTransient<ICommandHandler<TriageContext>, TriageHandler>();
        services.AddTransient<DeterministicTriageSelector>();
        services.AddTransient<TriageLabelOverrideApplier>();
        services.AddTransient<ProjectMapExcerptBuilder>();
        services.AddTransient<PhaseCommandExpander>();
        services.AddSingleton<SinglePhaseCollapser>();
        services.AddTransient<ITriageOutputProducer, TriageOutputProducer>();
        services.AddTransient<StructuredTriageStrategy>();
        services.AddTransient<ITriageStrategySelector, TriageStrategySelector>();
        services.AddTransient<ICommandHandler<LoadSkillsContext>, LoadSkillsHandler>();
        services.AddSingleton<ActivationExpressionTokenizer>();
        services.AddSingleton<ActivationExpressionParser>();
        services.AddSingleton<ActivationEvaluator>();
        services.AddSingleton<ActivationSkillFilter>();
        services.AddSingleton<ActivationSpecificityScorer>();
        services.AddSingleton<PhaseSpecificityTrimmer>();
        AddConceptPublishingHandler<PipelineNameInitializerHandler, PipelineNameInitializerContext>(services);
        AddConceptPublishingHandler<BootstrapCheckHandler, BootstrapCheckContext>(services);
        services.AddTransient<ICommandHandler<BootstrapGateContext>, BootstrapGateHandler>();
        AddConceptPublishingHandler<PublishProjectLanguageHandler, PublishProjectLanguageContext>(services);
        services.AddTransient<ICommandHandler<BootstrapDispatchContext>, BootstrapDispatchHandler>();
        services.AddTransient<ICommandHandler<BootstrapDiscoverContext>, BootstrapDiscoverHandler>();
        services.AddSingleton<ConceptWriterRegistry>();
        services.AddTransient<ICommandHandler<LoadSwaggerContext>, LoadSwaggerHandler>();
        services.AddTransient<ICommandHandler<SpawnNucleiContext>, SpawnNucleiHandler>();
        services.AddTransient<ICommandHandler<SpawnSpectralContext>, SpawnSpectralHandler>();
        services.AddTransient<ICommandHandler<SpawnZapContext>, SpawnZapHandler>();
        services.AddTransient<ICommandHandler<CompileFindingsContext>, CompileFindingsHandler>();
        services.AddSingleton<IMasterOutputSchemaResolver, MasterOutputSchemaResolver>();
        services.AddSingleton<IScanMasterPromptFactory, ScanMasterPromptFactory>();
        services.AddTransient<ICommandHandler<CollectMasterFindingsContext>, CollectMasterFindingsHandler>();
        services.AddTransient<ICommandHandler<DeliverFindingsContext>, DeliverFindingsHandler>();
        services.AddTransient<ICommandHandler<StaticPatternScanContext>, StaticPatternScanHandler>();
        services.AddTransient<ICommandHandler<GitHistoryScanContext>, GitHistoryScanHandler>();
        services.AddTransient<ICommandHandler<DependencyAuditContext>, DependencyAuditHandler>();
        services.AddTransient<ICommandHandler<CompressSecurityFindingsContext>, CompressSecurityFindingsHandler>();
        services.AddTransient<ICommandHandler<MergeMasterFindingsContext>, MergeMasterFindingsHandler>();
        services.AddTransient<NucleiTopSelector>();
        services.AddTransient<ZapTopSelector>();
        services.AddTransient<SpectralTopSelector>();
        services.AddTransient<ICommandHandler<CompressApiScanFindingsContext>, CompressApiScanFindingsHandler>();
        services.AddTransient<ICommandHandler<SecurityTrendContext>, SecurityTrendHandler>();
        services.AddTransient<ICommandHandler<SecuritySnapshotWriteContext>, SecuritySnapshotWriter>();
        services.AddTransient<ICommandHandler<SpawnFixContext>, SpawnFixHandler>();
        services.AddSingleton<HttpProbeRunner>();
        return services;
    }

    // Triple-registration: concrete handler + ICommandHandler<TContext> + IConceptWriter
    // singleton-of-handler so the validate-concepts registry sees the writer claim
    // without changing the transient lifetime used at the pipeline-execution path.
    private static void AddConceptPublishingHandler<THandler, TContext>(IServiceCollection services)
        where THandler : class, ICommandHandler<TContext>, IConceptWriter
        where TContext : ICommandContext
    {
        services.AddTransient<THandler>();
        services.AddTransient<ICommandHandler<TContext>>(sp => sp.GetRequiredService<THandler>());
        services.AddSingleton<IConceptWriter>(sp => sp.GetRequiredService<THandler>());
    }
}
