using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Tickets;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.AddTransient<Scope.RemoteContextInventoryBuilder>();
        // p0413: the classifier's size + shape estimates become run state here.
        services.AddTransient<Scope.ScopeEstimateRecorder>();
        AddConceptPublishingHandler<CheckoutSourceHandler, CheckoutSourceContext>(services);
        // p0331: shared clone-into-sandbox path (CheckoutSource + ensure_repo_sandbox)
        // and the per-run factory for the master's escalation tool host.
        services.AddTransient<SandboxRepoCloner>();
        services.AddTransient<Tools.EnsureRepoSandboxToolFactory>();
        AddConceptPublishingHandler<TryCheckoutSourceHandler, TryCheckoutSourceContext>(services);
        services.AddTransient<ICommandHandler<SetupRegistryAuthContext>, SetupRegistryAuthHandler>();
        // p0375: generic registry-auth staging for ecosystems the deterministic
        // NuGet/npm fast-paths do not cover — declared/persisted registry_auth
        // template first, Scout-role LLM fallback second, token substituted host-side.
        services.AddTransient<Registry.RegistryHostGrep>();
        services.AddTransient<Registry.StagedAuthFileJsonReader>();
        services.AddTransient<Registry.IRegistryAuthStager, Registry.RegistryAuthStager>();
        services.AddTransient<Registry.RegistryAuthPathGuard>();
        services.AddTransient<Registry.RegistryTokenSubstitutor>();
        services.AddTransient<Registry.SecretLeakGuard>();
        services.AddTransient<Registry.StagedAuthFileWriter>();
        services.AddTransient<Registry.RegistryAuthFailureReporter>();
        services.AddTransient<Registry.RegistryAuthTemplateStore>();
        services.AddTransient<Registry.GenericRegistryAuthApplier>();
        services.AddTransient<ICommandHandler<EnsurePrerequisitesContext>, EnsurePrerequisitesHandler>();
        services.AddTransient<ICommandHandler<LoadCodingPrinciplesContext>, LoadCodingPrinciplesHandler>();
        // p0380: plan-time experiential-memory index + green-run narrative twin.
        services.AddTransient<ICommandHandler<LoadMemoryIndexContext>, LoadMemoryIndexHandler>();
        services.AddTransient<Memory.RunNarrativeMemoryWriter>();
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
        // p0393a: turn the ticket into an ordered SET of phase specs after AnalyzeCode —
        // deriver (the one LLM call, judgement only), deterministic segmenter/extractor,
        // reader + writer over the ticket branch, publisher (commit, pointer, draft PR),
        // and the sequence that splices one block per phase.
        services.AddTransient<ICommandHandler<DeriveSpecContext>, DeriveSpecHandler>();
        services.AddTransient<ICommandHandler<PhaseSequenceContext>, PhaseSequenceHandler>();
        services.AddTransient<ICommandHandler<SelectPhaseContext>, SelectPhaseHandler>();
        services.AddTransient<ICommandHandler<SpecHandbackContext>, SpecHandbackHandler>();
        services.AddSpecDerivation();
        services.AddTransient<DiscoveryOutputParser>();
        // p0403: statics that needed a collaborator are services now.
        services.AddTransient<RunDirectoryReader>();
        services.AddTransient<SnapshotYamlParser>();
        services.AddTransient<ObservationRecoveryHelper>();
        services.AddTransient<Lifecycle.TicketLifecycle>();
        services.AddTransient<ProjectMapCacheKey>();
        services.AddSingleton<SandboxTargets>();
        services.AddSingleton<Tools.AgenticToolSurface>();
        services.AddSingleton<Polling.PipelineResolver>();
        // p0401: shared scanner-observation service (severity mapping + warn-once).
        services.AddSingleton<ScannerObservationFactory>();
        services.AddTransient<ICommandHandler<ApprovalContext>, ApprovalHandler>();
        services.AddTransient<ICommandHandler<AgenticMasterContext>, AgenticMasterHandler>();
        services.AddTransient<ITicketDocumentMaterializer, TicketDocumentMaterializer>();
        services.AddTransient<SandboxGitOperations>();
        // p0411: the framework-owned sandbox facts — the committing identity (set at
        // checkout) and the working tree's changed paths (carried in the state block).
        services.AddTransient<Sandbox.SandboxGitIdentity>();
        services.AddTransient<Sandbox.SandboxWorkingTreeReader>();
        services.AddTransient<RunWorkCheckpointer>(); // p0360: mid-run work durability
        services.AddSingleton<ISecretPatternScanner, SecretPatternScanner>();
        services.AddTransient<ICommandHandler<CommitAndPRContext>, CommitAndPRHandler>();
        services.AddTransient<ICommandHandler<LoadContextContext>, LoadContextHandler>();
        services.AddTransient<ICommandHandler<WriteRunResultContext>, WriteRunResultHandler>();
        services.AddTransient<ICommandHandler<InitCommitContext>, InitCommitHandler>();
        services.AddTransient<ICommandHandler<PrCrossLinkContext>, PrCrossLinkHandler>();
        services.AddTransient<ICommandHandler<SwitchSkillContext>, SwitchSkillHandler>();
        services.AddTransient<ICommandHandler<PersistWorkBranchContext>, PersistWorkBranchHandler>();
        services.AddTransient<ICommandHandler<GenerateTestsContext>, GenerateTestsHandler>();
        services.AddTransient<ICommandHandler<GenerateDocsContext>, GenerateDocsHandler>();
        // p0355: scopes the test/doc passes to the repos that actually changed.
        services.AddTransient<RepoDiffPartitioner>();
        services.AddTransient<ICommandHandler<AcquireSourceContext>, AcquireSourceHandler>();
        services.AddTransient<ICommandHandler<BootstrapDocumentContext>, BootstrapDocumentHandler>();
        services.AddTransient<ICommandHandler<DeliverOutputContext>, DeliverOutputHandler>();
        services.AddTransient<ICommandHandler<SessionSetupContext>, SessionSetupHandler>();
        services.AddTransient<ICommandHandler<AskContext>, AskCommandHandler>();
        services.AddTransient<ICommandHandler<CompileKnowledgeContext>, CompileKnowledgeHandler>();
        services.AddTransient<ICommandHandler<QueryKnowledgeContext>, QueryKnowledgeHandler>();
        services.AddSingleton<KnowledgePromptBuilder>();
        services.AddTransient<IGateOutputHandler, GateOutputHandler>();
        services.AddTransient<IGateRetryCoordinator, GateRetryCoordinator>();
        services.AddTransient<IUpstreamContextBuilder, UpstreamContextBuilder>();
        services.AddTransient<ICommandHandler<LoadSkillsContext>, LoadSkillsHandler>();
        services.AddSingleton<ActivationExpressionTokenizer>();
        services.AddSingleton<ActivationExpressionParser>();
        services.AddSingleton<ActivationEvaluator>();
        services.AddSingleton<ActivationSkillFilter>();
        services.AddSingleton<ActivationSpecificityScorer>();
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
        services.AddTransient<IPhaseExecutionPromptFactory, PhaseExecutionPromptFactory>();
        services.AddTransient<ICommandHandler<PhaseSpecGateContext>, PhaseSpecGateHandler>();
        services.AddTransient<VerifyCommandRunner>(); // p0419
        services.AddTransient<ICommandHandler<VerifyPhaseContext>, VerifyPhaseHandler>(); // p0393
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
