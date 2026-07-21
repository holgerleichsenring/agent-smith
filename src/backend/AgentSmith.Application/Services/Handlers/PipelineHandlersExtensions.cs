using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tickets;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Sandbox;
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
        // p0331: ticket→repo scope classification + pre-checkout context inventory.
        services.AddTransient<ICommandHandler<ScopeReposContext>, ScopeReposHandler>();
        services.AddTransient<Scope.RepoScopeClassifier>();
        AddConceptPublishingHandler<CheckoutSourceHandler, CheckoutSourceContext>(services);
        // p0331: shared clone-into-sandbox path (CheckoutSource + ensure_repo_sandbox)
        // and the per-run factory for the master's escalation tool host.
        services.AddTransient<SandboxRepoCloner>();
        services.AddTransient<Tools.EnsureRepoSandboxToolFactory>();
        AddConceptPublishingHandler<TryCheckoutSourceHandler, TryCheckoutSourceContext>(services);
        services.AddTransient<ICommandHandler<SetupRegistryAuthContext>, SetupRegistryAuthHandler>();
        services.AddTransient<ICommandHandler<EnsurePrerequisitesContext>, EnsurePrerequisitesHandler>();
        services.AddTransient<ICommandHandler<LoadCodingPrinciplesContext>, LoadCodingPrinciplesHandler>();
        services.AddTransient<ICommandHandler<AnalyzeCodeContext>, AnalyzeProjectHandler>();
        services.AddTransient<IProjectMapJsonReader, ProjectMapJsonReader>();
        services.AddTransient<IProjectAnalyzer, ProjectAnalyzer>();
        services.AddTransient<ICommandHandler<AnalyzePrDiffContext>, AnalyzePrDiffHandler>();
        services.AddTransient<IUnifiedDiffParser, UnifiedDiffParser>();
        services.AddTransient<ICommandHandler<CompilePrReviewFindingsContext>, CompilePrReviewFindingsHandler>();
        services.AddTransient<PrReviewFindingSelector>();
        services.AddTransient<PrReviewCommentRenderer>();
        services.AddTransient<ICommandHandler<PostPrCommentsContext>, PostPrCommentsHandler>();
        services.AddTransient<ICommandHandler<EmptyPlanCheckContext>, EmptyPlanCheckHandler>();
        // p0328: expectation negotiation — drafter (LLM + caps validation),
        // ratification question, edit parsing, tracker comment, outcome event.
        services.AddTransient<ICommandHandler<NegotiateExpectationContext>, NegotiateExpectationHandler>();
        services.AddTransient<Expectations.IExpectationDrafter, Expectations.ExpectationDrafter>();
        services.AddTransient<Expectations.ExpectationDraftValidator>();
        services.AddTransient<Expectations.ExpectationRatifier>();
        services.AddTransient<Expectations.IExpectationTrackerCommenter, Expectations.ExpectationTrackerCommenter>();
        services.AddTransient<Expectations.ExpectationOutcomeRecorder>();
        services.AddTransient<ExpectationQuestionBuilder>();
        services.AddTransient<ICommandHandler<ApprovalContext>, ApprovalHandler>();
        services.AddTransient<ICommandHandler<AgenticExecuteContext>, AgenticExecuteHandler>();
        services.AddTransient<ICommandHandler<AgenticMasterContext>, AgenticMasterHandler>();
        services.AddTransient<ITicketDocumentMaterializer, TicketDocumentMaterializer>();
        services.AddTransient<SandboxGitOperations>();
        services.AddTransient<RunWorkCheckpointer>(); // p0360: mid-run work durability
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
        // p0355: scopes the test/doc passes to the repos that actually changed.
        services.AddTransient<RepoDiffPartitioner>();
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
        // p0315b: spec-dialog — transcript prompt, phase-spec draft gate, tier-1
        // cached code map, reply hand-back, lazy read-only source sandboxes.
        services.AddTransient<ISpecDialogPromptFactory, SpecDialogPromptFactory>();
        services.AddSingleton<PhaseSpecSchemaProvider>();
        services.AddTransient<ISpecDraftValidator, SpecDraftValidator>();
        // p0315e: typed terminal outcome (answer | bug | phase | epic) —
        // resolver + per-kind parsers + epic requires-edge consistency.
        services.AddTransient<PhaseDraftReader>();
        services.AddTransient<BugOutcomeParser>();
        services.AddTransient<EpicOutcomeParser>();
        services.AddTransient<RequiresEdgeChecker>();
        services.AddTransient<IOutcomeProposalResolver, OutcomeProposalResolver>();
        services.AddTransient<ICommandHandler<LoadCachedCodeMapContext>, LoadCachedCodeMapHandler>();
        services.AddTransient<ICommandHandler<CollectSpecDialogReplyContext>, CollectSpecDialogReplyHandler>();
        // p0315d: phase-execution — spec extraction gate (inverse of the p0315c
        // renderer), spec-first master prompt, mid-run clarification park and the
        // phases/done/ dogfood record.
        services.AddTransient<IPhaseSpecFromTicket, PhaseSpecFromTicket>();
        services.AddTransient<PhaseSpecPlanFactory>();
        services.AddTransient<IPhaseExecutionPromptFactory, PhaseExecutionPromptFactory>();
        services.AddTransient<ICommandHandler<PhaseSpecGateContext>, PhaseSpecGateHandler>();
        services.AddTransient<ICommandHandler<MasterOpenQuestionsContext>, MasterOpenQuestionsHandler>();
        services.AddTransient<ICommandHandler<WritePhaseRecordContext>, WritePhaseRecordHandler>();
        services.AddTransient<ISourceScopeSandboxFactory, SourceScopeSandboxFactory>();
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
