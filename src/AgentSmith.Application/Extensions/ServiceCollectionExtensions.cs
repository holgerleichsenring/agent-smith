using AgentSmith.Application.Models;
using AgentSmith.Application.Prompts;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

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
        RegisterHandlers(services);
        RegisterContextBuilders(services);
        RegisterPipeline(services);
        return services;
    }

    private static void RegisterHandlers(IServiceCollection services)
    {
        services.AddTransient<ICommandHandler<FetchTicketContext>, FetchTicketHandler>();
        services.AddTransient<ICommandHandler<CheckoutSourceContext>, CheckoutSourceHandler>();
        services.AddTransient<ICommandHandler<LoadCodingPrinciplesContext>, LoadCodingPrinciplesHandler>();
        services.AddTransient<ICommandHandler<AnalyzeCodeContext>, AnalyzeProjectHandler>();
        services.AddTransient<IProjectAnalyzer, ProjectAnalyzer>();
        services.AddTransient<ICommandHandler<GeneratePlanContext>, GeneratePlanHandler>();
        services.AddTransient<ICommandHandler<ApprovalContext>, ApprovalHandler>();
        services.AddTransient<ICommandHandler<AgenticExecuteContext>, AgenticExecuteHandler>();
        services.AddTransient<TrxResultParser>();
        services.AddTransient<SandboxGitOperations>();
        services.AddTransient<ICommandHandler<TestContext>, TestHandler>();
        services.AddTransient<ICommandHandler<CommitAndPRContext>, CommitAndPRHandler>();
        services.AddTransient<ICommandHandler<BootstrapProjectContext>, BootstrapProjectHandler>();
        services.AddTransient<ICommandHandler<LoadCodeMapContext>, LoadCodeMapHandler>();
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
        services.AddTransient<TriageRationaleParser>();
        services.AddTransient<TriageOutputValidator>();
        services.AddTransient<TriageLabelOverrideApplier>();
        services.AddTransient<ProjectMapExcerptBuilder>();
        services.AddTransient<PhaseCommandExpander>();
        services.AddTransient<ITriageOutputProducer, TriageOutputProducer>();
        services.AddTransient<LegacyTriageStrategy>();
        services.AddTransient<StructuredTriageStrategy>();
        services.AddTransient<ITriageStrategySelector, TriageStrategySelector>();
        services.AddTransient<ICommandHandler<PhaseAdvanceContext>, PhaseAdvanceHandler>();
        services.AddTransient<ICommandHandler<PersistWorkBranchContext>, PersistWorkBranchHandler>();
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
        services.AddTransient<ICommandHandler<ApiCodeContextCommandContext>, ApiCodeContextHandler>();
        services.AddTransient<ICommandHandler<TryCheckoutSourceContext>, TryCheckoutSourceHandler>();
        services.AddTransient<ICommandHandler<CorrelateFindingsContext>, CorrelateFindingsHandler>();
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
        services.AddTransient<ICommandHandler<ExtractFindingsContext>, ExtractFindingsHandler>();
        services.AddTransient<ICommandHandler<SecurityTrendContext>, SecurityTrendHandler>();
        services.AddTransient<ICommandHandler<SecuritySnapshotWriteContext>, SecuritySnapshotWriter>();
        services.AddTransient<ICommandHandler<AskContext>, AskCommandHandler>();
        services.AddTransient<ICommandHandler<SpawnFixContext>, SpawnFixHandler>();
        services.AddTransient<ICommandHandler<DiscoverSkillsContext>, DiscoverSkillsHandler>();
        services.AddTransient<ICommandHandler<EvaluateSkillsContext>, EvaluateSkillsHandler>();
        services.AddTransient<ICommandHandler<DraftSkillFilesContext>, DraftSkillFilesHandler>();
        services.AddTransient<ICommandHandler<ApproveSkillsContext>, ApproveSkillsHandler>();
        services.AddTransient<ICommandHandler<InstallSkillsContext>, InstallSkillsHandler>();
        services.AddSingleton<KnowledgePromptBuilder>();
        services.AddSingleton<StructuredOutputInstructionBuilder>();
        services.AddTransient<ICommandHandler<CompileKnowledgeContext>, CompileKnowledgeHandler>();
        services.AddTransient<ICommandHandler<QueryKnowledgeContext>, QueryKnowledgeHandler>();
        services.AddTransient<ICommandHandler<LoadRunsContext>, LoadRunsHandler>();
        services.AddTransient<ICommandHandler<WriteTicketsContext>, WriteTicketsHandler>();
        services.AddTransient<MetaFileBootstrapper>();
        services.AddSingleton<HttpProbeRunner>();
    }

    private static void RegisterContextBuilders(IServiceCollection services)
    {
        AddBuilder<FetchTicketContextBuilder>(services, CommandNames.FetchTicket);
        AddBuilder<CheckoutSourceContextBuilder>(services, CommandNames.CheckoutSource);
        AddBuilder<TryCheckoutSourceContextBuilder>(services, CommandNames.TryCheckoutSource);
        AddBuilder<LoadCodingPrinciplesContextBuilder>(services, CommandNames.LoadCodingPrinciples);
        AddBuilder<LoadContextContextBuilder>(services, CommandNames.LoadContext);
        AddBuilder<LoadCodeMapContextBuilder>(services, CommandNames.LoadCodeMap);
        AddBuilder<BootstrapProjectContextBuilder>(services, CommandNames.BootstrapProject);
        AddBuilder<AnalyzeCodeContextBuilder>(services, CommandNames.AnalyzeCode);
        AddBuilder<GeneratePlanContextBuilder>(services, CommandNames.GeneratePlan);
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
        AddBuilder<ApiCodeContextContextBuilder>(services, CommandNames.ApiCodeContext);
        AddBuilder<CorrelateFindingsContextBuilder>(services, CommandNames.CorrelateFindings);
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
        AddBuilder<ExtractFindingsContextBuilder>(services, CommandNames.ExtractFindings);
        AddBuilder<SecurityTrendContextBuilder>(services, CommandNames.SecurityTrend);
        AddBuilder<SecuritySnapshotWriteContextBuilder>(services, CommandNames.SecuritySnapshotWrite);
        AddBuilder<AskContextBuilder>(services, CommandNames.Ask);
        AddBuilder<SpawnFixContextBuilder>(services, CommandNames.SpawnFix);
        AddBuilder<DiscoverSkillsContextBuilder>(services, CommandNames.DiscoverSkills);
        AddBuilder<EvaluateSkillsContextBuilder>(services, CommandNames.EvaluateSkills);
        AddBuilder<DraftSkillFilesContextBuilder>(services, CommandNames.DraftSkillFiles);
        AddBuilder<ApproveSkillsContextBuilder>(services, CommandNames.ApproveSkills);
        AddBuilder<InstallSkillsContextBuilder>(services, CommandNames.InstallSkills);
        AddBuilder<CompileKnowledgeContextBuilder>(services, CommandNames.CompileKnowledge);
        AddBuilder<QueryKnowledgeContextBuilder>(services, CommandNames.QueryKnowledge);
        AddBuilder<LoadRunsContextBuilder>(services, CommandNames.LoadRuns);
        AddBuilder<WriteTicketsContextBuilder>(services, CommandNames.WriteTickets);
    }

    private static void AddBuilder<TBuilder>(IServiceCollection services, string commandName)
        where TBuilder : IContextBuilder, new()
        => services.AddSingleton(new KeyedContextBuilder(commandName, new TBuilder()));

    private static void RegisterPipeline(IServiceCollection services)
    {
        services.AddTransient<IIntentParser, RegexIntentParser>();
        services.AddTransient<ICommandContextFactory, CommandContextFactory>();
        services.AddTransient<IPipelineExecutor, PipelineExecutor>();
        services.AddSingleton<SandboxSpecBuilder>();
        services.AddTransient<ISourceConfigOverrider, SourceConfigOverrider>();
        services.AddSingleton<IPipelineConfigResolver, PipelineConfigResolver>();
        services.AddTransient<ExecutePipelineUseCase>();
        // ITicketClaimService moved to Server.AddCoreDispatcherServices in p0109a — it
        // depends on IRedisJobQueue + IRedisClaimLock + IJobHeartbeatService, none of
        // which are in the CLI graph. Application's PipelineExecutor delegates lifecycle
        // wrapping to IPipelineLifecycleCoordinator (NoOp by default; Server overrides).
        services.AddSingleton<IPipelineLifecycleCoordinator, NoOpPipelineLifecycleCoordinator>();
        services.AddSingleton<Services.Prompts.AgentPromptBuilder>();
        services.AddSingleton<ISandboxFileReaderFactory, SandboxFileReaderFactory>();
    }
}
