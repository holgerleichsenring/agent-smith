using AgentSmith.Application.Models;
using AgentSmith.Application.Prompts;
using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Configuration;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.PipelineDataFlows;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Contracts.Pipeline;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application;

/// <summary>
/// Registers all application services (commands, handlers, pipeline, use cases) with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentSmithCommands(this IServiceCollection services)
    {
        // Transient (not Singleton): a Singleton CommandExecutor would receive the
        // ROOT IServiceProvider regardless of where it's resolved from, so its
        // GetService<ICommandHandler<T>>() calls would resolve handlers from the
        // root scope. Handlers like GeneratePlanHandler depend on Scoped services
        // (IAgentProviderFactory), so this trips ValidateScopes at runtime. Transient
        // means CommandExecutor injects the same provider its caller was resolved
        // from — Server flows resolve through a request scope, CLI flows resolve
        // from root and have no Scoped deps that matter (no DialogueTrail).
        services.AddTransient<ICommandExecutor, CommandExecutor>();
        services.AddSingleton<IPromptOverrideSource, EnvDirectoryPromptOverrideSource>();
        services.AddSingleton<IPromptCatalog, EmbeddedPromptCatalog>();
        services.AddSingleton<AgentSmithConfigValidator>();
        services.AddSingleton<Services.Configuration.PollingConfigDeprecationWarner>();
        RegisterHandlers(services);
        RegisterContextBuilders(services);
        RegisterPipeline(services);
        RegisterLoopServices(services);
        AddWebhookCommentIntent(services);
        return services;
    }

    // p0146e: CommentIntentParser is stateless — slash regexes + an IIntentParser
    // delegate. Singleton so the singleton PR-comment webhook handlers can take
    // it as a constructor dependency without a scope mismatch. The transient
    // IIntentParser is captured once at construction; LlmIntentParser holds no
    // mutable state (only DI-resolved factories + logger), so capture is safe.
    private static void AddWebhookCommentIntent(IServiceCollection services)
    {
        services.AddSingleton<CommentIntentParser>();
    }

    // p0126b: skill-call collaborator services. PipelineConcurrencyGate is scoped
    // (one per pipeline-run DI scope); OutcomeClassifier, RetryCoordinator and the
    // default NoOpSkillOutputValidator are stateless singletons.
    private static void RegisterLoopServices(IServiceCollection services)
    {
        services.AddScoped<PipelineConcurrencyGate>();
        services.AddSingleton<OutcomeClassifier>();
        services.AddSingleton<NoOpSkillOutputValidator>();
        services.AddSingleton<ISkillOutputValidator>(sp => sp.GetRequiredService<NoOpSkillOutputValidator>());
        services.AddSingleton<RetryCoordinator>();
        // p0147b: stateless factory that maps Incomplete/FailedRuntime outcomes
        // into typed execution-limit / execution-error SkillObservations so
        // silent skill drops become pipeline-visible.
        services.AddSingleton<RuntimeObservationFactory>();
        // p0126c: SkillCallRuntime is scoped (one per pipeline run); composes the
        // five collaborator services into the public ExecuteAsync flow.
        services.AddScoped<ISkillCallRuntime, SkillCallRuntime>();
        // p0128a: schema validators + factory. JsonSchemaLoader caches all four
        // hand-written schemas at boot for the process lifetime.
        services.AddSingleton<JsonSchemaLoader>();
        services.AddSingleton<PlanOutputValidator>();
        services.AddSingleton<DiffOutputValidator>();
        services.AddSingleton<BootstrapOutputValidator>();
        services.AddSingleton<ObservationOutputValidator>();
        services.AddSingleton<SkillOutputValidatorFactory>();
        // p0128a: in-memory store is the safe default; AgentSmith.Cli/Server's
        // Redis-gated registration replaces this with RedisRunArtifactStore when
        // a ConnectionMultiplexer is available.
        services.TryAddSingleton<IRunArtifactStore>(_ => new InMemoryRunArtifactStore());
    }

    private static void RegisterHandlers(IServiceCollection services)
    {
        services.AddTransient<ICommandHandler<FetchTicketContext>, FetchTicketHandler>();
        // p0125d: CheckoutSourceHandler also exposes IConceptWriter — three-step registration
        // (concrete + interface + singleton-IConceptWriter) so the validate-concepts registry
        // sees it without changing the transient lifetime used by the pipeline executor.
        services.AddTransient<CheckoutSourceHandler>();
        services.AddTransient<ICommandHandler<CheckoutSourceContext>>(sp =>
            sp.GetRequiredService<CheckoutSourceHandler>());
        services.AddSingleton<IConceptWriter>(sp =>
            sp.GetRequiredService<CheckoutSourceHandler>());
        services.AddTransient<ICommandHandler<LoadCodingPrinciplesContext>, LoadCodingPrinciplesHandler>();
        services.AddTransient<ICommandHandler<AnalyzeCodeContext>, AnalyzeProjectHandler>();
        services.AddTransient<IProjectAnalyzer, ProjectAnalyzer>();
        services.AddTransient<ICommandHandler<GeneratePlanContext>, GeneratePlanHandler>();
        services.AddTransient<ICommandHandler<EmptyPlanCheckContext>, EmptyPlanCheckHandler>();
        services.AddTransient<ICommandHandler<ApprovalContext>, ApprovalHandler>();
        services.AddTransient<ICommandHandler<AgenticExecuteContext>, AgenticExecuteHandler>();
        services.AddTransient<TrxResultParser>();
        services.AddTransient<SandboxGitOperations>();
        services.AddTransient<ICommandHandler<TestContext>, TestHandler>();
        services.AddTransient<ICommandHandler<CommitAndPRContext>, CommitAndPRHandler>();
        services.AddTransient<ICommandHandler<LoadContextContext>, LoadContextHandler>();
        services.AddTransient<ICommandHandler<WriteRunResultContext>, WriteRunResultHandler>();
        services.AddTransient<ICommandHandler<InitCommitContext>, InitCommitHandler>();
        services.AddTransient<ICommandHandler<TriageContext>, TriageHandler>();
        services.AddTransient<ICommandHandler<SwitchSkillContext>, SwitchSkillHandler>();
        services.AddTransient<PromptPrefixBuilder>();
        services.AddTransient<ISkillPromptBuilder, SkillPromptBuilder>();
        services.AddTransient<IGateOutputHandler, GateOutputHandler>();
        services.AddTransient<IGateRetryCoordinator, GateRetryCoordinator>();
        services.AddTransient<IUpstreamContextBuilder, UpstreamContextBuilder>();
        services.AddTransient<ICommandHandler<SkillRoundContext>, SkillRoundHandler>();
        services.AddTransient<ICommandHandler<SecuritySkillRoundContext>, SecuritySkillRoundHandler>();
        services.AddTransient<ICommandHandler<FilterRoundContext>, FilterRoundHandler>();

        // p0111c phase-based triage machinery
        // p0143: TriageRationaleParser + TriageOutputValidator retired (selector is by-construction valid).
        services.AddTransient<DeterministicTriageSelector>();
        services.AddTransient<TriageLabelOverrideApplier>();
        services.AddTransient<ProjectMapExcerptBuilder>();
        services.AddTransient<PhaseCommandExpander>();
        services.AddSingleton<SinglePhaseCollapser>();          // p0131c-pre
        services.AddTransient<ITriageOutputProducer, TriageOutputProducer>();
        services.AddTransient<StructuredTriageStrategy>();
        services.AddTransient<ITriageStrategySelector, TriageStrategySelector>();
        services.AddTransient<ICommandHandler<PhaseAdvanceContext>, PhaseAdvanceHandler>();
        // p0129a: Verify phase
        services.AddTransient<ICommandHandler<RunVerifyPhaseContext>, VerifyRoundHandler>();
        services.AddTransient<ICommandHandler<PersistWorkBranchContext>, PersistWorkBranchHandler>();
        // p0128b: Plan open-questions round-trip
        services.AddSingleton<PlanAnswerParser>();
        services.AddSingleton<IPlanOpenQuestionsPoster, PlanOpenQuestionsPoster>();
        services.AddTransient<ICommandHandler<PlanOpenQuestionsContext>, PlanOpenQuestionsHandler>();
        services.AddTransient<PlanConsolidator>();
        services.AddTransient<ICommandHandler<ConvergenceCheckContext>, ConvergenceCheckHandler>();
        services.AddTransient<ICommandHandler<GenerateTestsContext>, GenerateTestsHandler>();
        services.AddTransient<ICommandHandler<GenerateDocsContext>, GenerateDocsHandler>();
        services.AddTransient<ICommandHandler<CompileDiscussionContext>, CompileDiscussionHandler>();
        services.AddTransient<ICommandHandler<AcquireSourceContext>, AcquireSourceHandler>();
        services.AddTransient<ICommandHandler<BootstrapDocumentContext>, BootstrapDocumentHandler>();
        services.AddTransient<ICommandHandler<DeliverOutputContext>, DeliverOutputHandler>();
        services.AddTransient<ICommandHandler<SessionSetupContext>, SessionSetupHandler>();
        services.AddTransient<ICommandHandler<LoadSwaggerContext>, LoadSwaggerHandler>();
        services.AddSwaggerSpecCompression();
        // p0125d: TryCheckoutSourceHandler dual-registered as IConceptWriter (see CheckoutSourceHandler note above).
        services.AddTransient<TryCheckoutSourceHandler>();
        services.AddTransient<ICommandHandler<TryCheckoutSourceContext>>(sp =>
            sp.GetRequiredService<TryCheckoutSourceHandler>());
        services.AddSingleton<IConceptWriter>(sp =>
            sp.GetRequiredService<TryCheckoutSourceHandler>());
        services.AddTransient<ICommandHandler<SpawnNucleiContext>, SpawnNucleiHandler>();
        services.AddTransient<ICommandHandler<SpawnSpectralContext>, SpawnSpectralHandler>();
        services.AddTransient<ICommandHandler<SpawnZapContext>, SpawnZapHandler>();
        services.AddTransient<ICommandHandler<ApiSecuritySkillRoundContext>, ApiSkillRoundHandler>();
        services.AddTransient<ICommandHandler<CompileFindingsContext>, CompileFindingsHandler>();
        services.AddTransient<ICommandHandler<LoadSkillsContext>, LoadSkillsHandler>();
        services.AddTransient<ICommandHandler<DeliverFindingsContext>, DeliverFindingsHandler>();
        services.AddTransient<ICommandHandler<StaticPatternScanContext>, StaticPatternScanHandler>();
        services.AddTransient<ICommandHandler<GitHistoryScanContext>, GitHistoryScanHandler>();
        services.AddTransient<ICommandHandler<DependencyAuditContext>, DependencyAuditHandler>();
        services.AddTransient<ICommandHandler<CompressSecurityFindingsContext>, CompressSecurityFindingsHandler>();
        services.AddTransient<ICommandHandler<CompressApiScanFindingsContext>, CompressApiScanFindingsHandler>();
        services.AddTransient<ICommandHandler<SecurityTrendContext>, SecurityTrendHandler>();
        services.AddTransient<ICommandHandler<SecuritySnapshotWriteContext>, SecuritySnapshotWriter>();
        services.AddTransient<ICommandHandler<AskContext>, AskCommandHandler>();
        services.AddTransient<ICommandHandler<SpawnFixContext>, SpawnFixHandler>();
        // p0144: skill-manager handler chain retired in favour of standard SkillRound.
        services.AddSingleton<KnowledgePromptBuilder>();
        services.AddSingleton<StructuredOutputInstructionBuilder>();
        services.AddTransient<ICommandHandler<CompileKnowledgeContext>, CompileKnowledgeHandler>();
        services.AddTransient<ICommandHandler<QueryKnowledgeContext>, QueryKnowledgeHandler>();
        services.AddTransient<ICommandHandler<LoadRunsContext>, LoadRunsHandler>();
        services.AddTransient<ICommandHandler<WriteTicketsContext>, WriteTicketsHandler>();
        services.AddSingleton<HttpProbeRunner>();

        // p0125b: activation expression pipeline (tokenizer/parser/evaluator are stateless,
        // so singleton is safe; no production runtime path consumes them yet — that's p0125c/d/p0127).
        services.AddSingleton<ActivationExpressionTokenizer>();
        services.AddSingleton<ActivationExpressionParser>();
        services.AddSingleton<ActivationEvaluator>();

        // p0127b: triage pre-filter + post-LLM specificity tie-break.
        services.AddSingleton<ActivationSkillFilter>();
        services.AddSingleton<ActivationSpecificityScorer>();
        services.AddSingleton<PhaseSpecificityTrimmer>();

        // p0125c/d: typed concept publication. Handlers are registered three times:
        //   1. concrete type (transient) — resolvable for IConceptWriter dual-registration
        //   2. ICommandHandler<TContext> — pipeline execution path
        //   3. IConceptWriter (singleton-of-handler) — build-time validate-concepts registry
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

        // p0130a: BootstrapGate is a policy handler — reads concepts published by
        // BootstrapCheckHandler and aborts the pipeline when bootstrap files are missing.
        services.AddTransient<ICommandHandler<BootstrapGateContext>, BootstrapGateHandler>();

        // p0130c: PublishProjectLanguage publishes the project_language enum (IConceptWriter)
        services.AddTransient<PublishProjectLanguageHandler>();
        services.AddTransient<ICommandHandler<PublishProjectLanguageContext>>(sp =>
            sp.GetRequiredService<PublishProjectLanguageHandler>());
        services.AddSingleton<IConceptWriter>(sp =>
            sp.GetRequiredService<PublishProjectLanguageHandler>());

        // p0130c: BootstrapDispatch deterministic SkillRound emit for init-project
        services.AddTransient<ICommandHandler<BootstrapDispatchContext>, BootstrapDispatchHandler>();

        // p0130c-followup: producer-loop runtime for bootstrap skills (csharp/
        // node/python/generic-bootstrap). Distinct from SkillRound because the
        // chat call carries WriteFile + the bootstrap PathWriteGuard.
        services.AddTransient<ICommandHandler<BootstrapRoundContext>, BootstrapRoundHandler>();

        services.AddSingleton<ConceptWriterRegistry>();
    }

    private static void RegisterContextBuilders(IServiceCollection services)
    {
        AddBuilder<FetchTicketContextBuilder>(services, CommandNames.FetchTicket);
        AddBuilder<CheckoutSourceContextBuilder>(services, CommandNames.CheckoutSource);
        AddBuilder<TryCheckoutSourceContextBuilder>(services, CommandNames.TryCheckoutSource);
        AddBuilder<LoadCodingPrinciplesContextBuilder>(services, CommandNames.LoadCodingPrinciples);
        AddBuilder<LoadContextContextBuilder>(services, CommandNames.LoadContext);
        AddBuilder<AnalyzeCodeContextBuilder>(services, CommandNames.AnalyzeCode);
        AddBuilder<GeneratePlanContextBuilder>(services, CommandNames.GeneratePlan);
        AddBuilder<EmptyPlanCheckContextBuilder>(services, CommandNames.EmptyPlanCheck);
        AddBuilder<ApprovalContextBuilder>(services, CommandNames.Approval);
        AddBuilder<AgenticExecuteContextBuilder>(services, CommandNames.AgenticExecute);
        AddBuilder<TestContextBuilder>(services, CommandNames.Test);
        AddBuilder<WriteRunResultContextBuilder>(services, CommandNames.WriteRunResult);
        AddBuilder<CommitAndPRContextBuilder>(services, CommandNames.CommitAndPR);
        AddBuilder<InitCommitContextBuilder>(services, CommandNames.InitCommit);
        AddBuilder<TriageContextBuilder>(services, CommandNames.Triage);
        AddBuilder<SwitchSkillContextBuilder>(services, CommandNames.SwitchSkill);
        AddBuilder<SkillRoundContextBuilder>(services, CommandNames.SkillRound);
        AddBuilder<SecuritySkillRoundContextBuilder>(services, CommandNames.SecuritySkillRound);
        AddBuilder<FilterRoundContextBuilder>(services, CommandNames.FilterRound);
        AddBuilder<RunReviewPhaseContextBuilder>(services, CommandNames.RunReviewPhase);
        AddBuilder<RunFinalPhaseContextBuilder>(services, CommandNames.RunFinalPhase);
        AddBuilder<RunVerifyPhaseContextBuilder>(services, CommandNames.RunVerifyPhase);
        AddBuilder<PersistWorkBranchContextBuilder>(services, CommandNames.PersistWorkBranch);
        AddBuilder<ConvergenceCheckContextBuilder>(services, CommandNames.ConvergenceCheck);
        AddBuilder<GenerateTestsContextBuilder>(services, CommandNames.GenerateTests);
        AddBuilder<GenerateDocsContextBuilder>(services, CommandNames.GenerateDocs);
        AddBuilder<CompileDiscussionContextBuilder>(services, CommandNames.CompileDiscussion);
        AddBuilder<AcquireSourceContextBuilder>(services, CommandNames.AcquireSource);
        AddBuilder<BootstrapDocumentContextBuilder>(services, CommandNames.BootstrapDocument);
        AddBuilder<DeliverOutputContextBuilder>(services, CommandNames.DeliverOutput);
        AddBuilder<SessionSetupContextBuilder>(services, CommandNames.SessionSetup);
        AddBuilder<LoadSwaggerContextBuilder>(services, CommandNames.LoadSwagger);
        AddBuilder<SpawnNucleiContextBuilder>(services, CommandNames.SpawnNuclei);
        AddBuilder<SpawnSpectralContextBuilder>(services, CommandNames.SpawnSpectral);
        AddBuilder<SpawnZapContextBuilder>(services, CommandNames.SpawnZap);
        AddBuilder<ApiSecuritySkillRoundContextBuilder>(services, CommandNames.ApiSecuritySkillRound);
        AddBuilder<CompileFindingsContextBuilder>(services, CommandNames.CompileFindings);
        AddBuilder<LoadSkillsContextBuilder>(services, CommandNames.LoadSkills);
        AddBuilder<DeliverFindingsContextBuilder>(services, CommandNames.DeliverFindings);
        AddBuilder<StaticPatternScanContextBuilder>(services, CommandNames.StaticPatternScan);
        AddBuilder<GitHistoryScanContextBuilder>(services, CommandNames.GitHistoryScan);
        AddBuilder<DependencyAuditContextBuilder>(services, CommandNames.DependencyAudit);
        AddBuilder<CompressSecurityFindingsContextBuilder>(services, CommandNames.CompressSecurityFindings);
        AddBuilder<CompressApiScanFindingsContextBuilder>(services, CommandNames.CompressApiScanFindings);
        AddBuilder<SecurityTrendContextBuilder>(services, CommandNames.SecurityTrend);
        AddBuilder<SecuritySnapshotWriteContextBuilder>(services, CommandNames.SecuritySnapshotWrite);
        AddBuilder<AskContextBuilder>(services, CommandNames.Ask);
        AddBuilder<SpawnFixContextBuilder>(services, CommandNames.SpawnFix);
        // p0144: skill-manager context-builders retired alongside the handlers.
        AddBuilder<CompileKnowledgeContextBuilder>(services, CommandNames.CompileKnowledge);
        AddBuilder<QueryKnowledgeContextBuilder>(services, CommandNames.QueryKnowledge);
        AddBuilder<LoadRunsContextBuilder>(services, CommandNames.LoadRuns);
        AddBuilder<WriteTicketsContextBuilder>(services, CommandNames.WriteTickets);
        AddBuilder<PipelineNameInitializerContextBuilder>(services, CommandNames.PipelineNameInitializer);
        AddBuilder<PlanOpenQuestionsContextBuilder>(services, CommandNames.PlanOpenQuestions);
        AddBuilder<BootstrapCheckContextBuilder>(services, CommandNames.BootstrapCheck);
        AddBuilder<BootstrapGateContextBuilder>(services, CommandNames.BootstrapGate);
        AddBuilder<PublishProjectLanguageContextBuilder>(services, CommandNames.PublishProjectLanguage);
        AddBuilder<BootstrapDispatchContextBuilder>(services, CommandNames.BootstrapDispatch);
        AddBuilder<BootstrapRoundContextBuilder>(services, CommandNames.BootstrapRound);
    }

    private static void AddBuilder<TBuilder>(IServiceCollection services, string commandName)
        where TBuilder : IContextBuilder, new()
        => services.AddSingleton(new KeyedContextBuilder(commandName, new TBuilder()));

    private static void RegisterPipeline(IServiceCollection services)
    {
        services.AddTransient<IIntentParser>(sp => new LlmIntentParser(
            sp.GetRequiredService<IChatClientFactory>(),
            sp.GetRequiredService<IConfigurationLoader>(),
            new AgentConfig { Type = "claude" },
            sp.GetRequiredService<ILogger<LlmIntentParser>>()));
        services.AddTransient<ICommandContextFactory, CommandContextFactory>();
        AddPipelineExecutor(services);

        // p0128c: data-flow gating. Each preset's IPhaseDataFlow is registered as a
        // singleton so the resolver builds an O(1) name→declaration index at startup.
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
        // p0140a: ProjectResolver is stateless. p0140b: exposed as IEnvelopeProjectResolver
        // for webhook handlers + SpawnPipelineRunsUseCase.
        services.AddSingleton<Services.Triggers.ProjectResolver>();
        services.AddSingleton<Contracts.Services.IEnvelopeProjectResolver>(
            sp => sp.GetRequiredService<Services.Triggers.ProjectResolver>());
        // p0140b: SpawnPipelineRunsUseCase is the only place that builds ClaimRequests from
        // a webhook envelope. Depends on ITicketClaimService (Server-only) so this service
        // resolves only inside the Server composition; CLI graph doesn't use it.
        services.AddTransient<Contracts.Services.ISpawnPipelineRunsUseCase, Services.Spawning.SpawnPipelineRunsUseCase>();
        services.AddTransient<ExecutePipelineUseCase>();
        // ITicketClaimService moved to Server.AddCoreDispatcherServices in p0109a — it
        // depends on IRedisJobQueue + IRedisClaimLock + IJobHeartbeatService, none of
        // which are in the CLI graph. Application's PipelineExecutor delegates lifecycle
        // wrapping to IPipelineLifecycleCoordinator (NoOp by default; Server overrides).
        services.AddSingleton<IPipelineLifecycleCoordinator, NoOpPipelineLifecycleCoordinator>();
        services.AddSingleton<Services.Prompts.AgentPromptBuilder>();
        services.AddSingleton<ISandboxFileReaderFactory, SandboxFileReaderFactory>();
        RegisterToolHosts(services);
    }

    // p0145: ToolKit + the default policy. IToolHost instances are NOT
    // DI-registered — each host carries per-pipeline-run state (sandbox,
    // decision logger, repo path, dialogue transport, job id) that lives in
    // PipelineContext, not DI. Callers construct hosts and pass them to
    // IToolKit.GetToolsFor at call time. ToolKit is stateless (only the
    // policy), so singleton is fine.
    private static void RegisterToolHosts(IServiceCollection services)
    {
        services.AddSingleton<IPipelineToolPolicy, AllHostsActivePolicy>();
        services.AddSingleton<IToolKit, ToolKit>();
    }

    // p0147e: PipelineExecutor decomposed into IPipelineStepRunner +
    // IPipelineErrorHandler + IPipelineSandboxCoordinator. The legacy
    // monolith is kept behind PIPELINE_EXECUTOR_USE_LEGACY env flag for
    // one release cycle so the test pack can run both shapes in parallel
    // and assert identical outcomes.
    //
    // Lifetime notes:
    //   - PipelineExecutor (orchestrator): transient — composes per-call
    //   - PipelineStepRunner: transient — uses ICommandExecutor which is itself transient
    //   - PipelineErrorHandler: transient — same scoping argument
    //   - PipelineSandboxCoordinator: transient — owns mutable per-run state
    //     (the cached ISandbox); singleton would share across overlapping runs.
    private static void AddPipelineExecutor(IServiceCollection services)
    {
        services.AddTransient<IPipelineStepRunner, PipelineStepRunner>();
        services.AddTransient<IPipelineErrorHandler, PipelineErrorHandler>();
        services.AddTransient<IPipelineSandboxCoordinator, PipelineSandboxCoordinator>();
        services.AddTransient<PipelineExecutor>();
        services.AddTransient<PipelineExecutorLegacy>();

        services.AddTransient<IPipelineExecutor>(sp =>
            UseLegacyExecutor()
                ? sp.GetRequiredService<PipelineExecutorLegacy>()
                : sp.GetRequiredService<PipelineExecutor>());
    }

    /// <summary>
    /// Feature flag for the p0147e parallel-class migration. Set
    /// <c>PIPELINE_EXECUTOR_USE_LEGACY=1</c> to fall back to the monolithic
    /// pre-p0147e shape; absence (or anything else) selects the decomposed
    /// executor. Slated for removal after one release cycle.
    /// </summary>
    private static bool UseLegacyExecutor()
    {
        var raw = Environment.GetEnvironmentVariable("PIPELINE_EXECUTOR_USE_LEGACY");
        return raw is "1" or "true" or "TRUE";
    }

    // p0147c: swagger-spec compression service. Stateless / threshold-gated, so
    // singleton is safe. Pulled into its own AddXxx helper per the spec-first
    // subdomain-DI convention.
    public static IServiceCollection AddSwaggerSpecCompression(this IServiceCollection services)
    {
        services.AddSingleton<ISwaggerSpecCompressor, SwaggerSpecCompressor>();
        return services;
    }
}
