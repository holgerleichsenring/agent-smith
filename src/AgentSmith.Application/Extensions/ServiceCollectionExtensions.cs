using AgentSmith.Application.Models;
using AgentSmith.Application.Prompts;
using AgentSmith.Contracts.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
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
        services.AddSingleton<ICommandExecutor, CommandExecutor>();
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
        services.AddTransient<ICommandHandler<LoadDomainRulesContext>, LoadDomainRulesHandler>();
        services.AddTransient<ICommandHandler<AnalyzeCodeContext>, AnalyzeCodeHandler>();
        services.AddTransient<ICommandHandler<GeneratePlanContext>, GeneratePlanHandler>();
        services.AddTransient<ICommandHandler<ApprovalContext>, ApprovalHandler>();
        services.AddTransient<ICommandHandler<AgenticExecuteContext>, AgenticExecuteHandler>();
        services.AddTransient<ICommandHandler<TestContext>, TestHandler>();
        services.AddTransient<ICommandHandler<CommitAndPRContext>, CommitAndPRHandler>();
        services.AddTransient<ICommandHandler<BootstrapProjectContext>, BootstrapProjectHandler>();
        services.AddTransient<ICommandHandler<LoadCodeMapContext>, LoadCodeMapHandler>();
        services.AddTransient<ICommandHandler<LoadContextContext>, LoadContextHandler>();
        services.AddTransient<ICommandHandler<WriteRunResultContext>, WriteRunResultHandler>();
        services.AddTransient<ICommandHandler<InitCommitContext>, InitCommitHandler>();
        services.AddTransient<ICommandHandler<TriageContext>, TriageHandler>();
        services.AddTransient<ICommandHandler<SecurityTriageContext>, SecurityTriageHandler>();
        services.AddTransient<ICommandHandler<SwitchSkillContext>, SwitchSkillHandler>();
        services.AddTransient<PromptPrefixBuilder>();
        services.AddTransient<ISkillPromptBuilder, SkillPromptBuilder>();
        services.AddTransient<IGateOutputHandler, GateOutputHandler>();
        services.AddTransient<IGateRetryCoordinator, GateRetryCoordinator>();
        services.AddTransient<IUpstreamContextBuilder, UpstreamContextBuilder>();
        services.AddTransient<ICommandHandler<SkillRoundContext>, SkillRoundHandler>();
        services.AddTransient<ICommandHandler<SecuritySkillRoundContext>, SecuritySkillRoundHandler>();
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
        services.AddTransient<ICommandHandler<SpawnNucleiContext>, SpawnNucleiHandler>();
        services.AddTransient<ICommandHandler<SpawnSpectralContext>, SpawnSpectralHandler>();
        services.AddTransient<ICommandHandler<SpawnZapContext>, SpawnZapHandler>();
        services.AddTransient<ApiSecurityTriagePromptBuilder>();
        services.AddTransient<ApiSecuritySkillFilter>();
        services.AddTransient<ICommandHandler<ApiSecurityTriageContext>, ApiSecurityTriageHandler>();
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
        services.AddTransient<ISkillGraphBuilder, SkillGraphBuilder>();
        services.AddSingleton<HttpProbeRunner>();
    }

    private static void RegisterContextBuilders(IServiceCollection services)
    {
        AddBuilder<FetchTicketContextBuilder>(services, CommandNames.FetchTicket);
        AddBuilder<CheckoutSourceContextBuilder>(services, CommandNames.CheckoutSource);
        AddBuilder<TryCheckoutSourceContextBuilder>(services, CommandNames.TryCheckoutSource);
        AddBuilder<LoadDomainRulesContextBuilder>(services, CommandNames.LoadDomainRules);
        AddBuilder<LoadDomainRulesContextBuilder>(services, CommandNames.LoadCodingPrinciples);
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
        AddBuilder<SecurityTriageContextBuilder>(services, CommandNames.SecurityTriage);
        AddBuilder<SwitchSkillContextBuilder>(services, CommandNames.SwitchSkill);
        AddBuilder<SkillRoundContextBuilder>(services, CommandNames.SkillRound);
        AddBuilder<SecuritySkillRoundContextBuilder>(services, CommandNames.SecuritySkillRound);
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
        AddBuilder<SpawnNucleiContextBuilder>(services, CommandNames.SpawnNuclei);
        AddBuilder<SpawnSpectralContextBuilder>(services, CommandNames.SpawnSpectral);
        AddBuilder<SpawnZapContextBuilder>(services, CommandNames.SpawnZap);
        AddBuilder<ApiSecurityTriageContextBuilder>(services, CommandNames.ApiSecurityTriage);
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
        services.AddTransient<ISourceConfigOverrider, SourceConfigOverrider>();
        services.AddTransient<ExecutePipelineUseCase>();
        services.AddScoped<ITicketClaimService, TicketClaimService>();
    }
}
