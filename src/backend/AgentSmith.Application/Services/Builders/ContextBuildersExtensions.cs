using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Builders;

/// <summary>
/// Keyed context-builder registrations — one per pipeline command name. The
/// KeyedContextBuilder wrapper lets CommandContextFactory resolve a builder by
/// CommandNames.X without an open-generic factory. p0144: skill-manager
/// context-builders retired alongside the handlers.
/// </summary>
public static class ContextBuildersExtensions
{
    public static IServiceCollection AddContextBuilders(this IServiceCollection services)
    {
        AddBuilder<FetchTicketContextBuilder>(services, CommandNames.FetchTicket);
        AddBuilder<CheckoutSourceContextBuilder>(services, CommandNames.CheckoutSource);
        AddBuilder<TryCheckoutSourceContextBuilder>(services, CommandNames.TryCheckoutSource);
        AddBuilder<SetupRegistryAuthContextBuilder>(services, CommandNames.SetupRegistryAuth);
        AddBuilder<EnsurePrerequisitesContextBuilder>(services, CommandNames.EnsurePrerequisites);
        AddBuilder<LoadCodingPrinciplesContextBuilder>(services, CommandNames.LoadCodingPrinciples);
        AddBuilder<LoadContextContextBuilder>(services, CommandNames.LoadContext);
        AddBuilder<AnalyzeCodeContextBuilder>(services, CommandNames.AnalyzeCode);
        AddBuilder<GeneratePlanContextBuilder>(services, CommandNames.GeneratePlan);
        AddBuilder<EmptyPlanCheckContextBuilder>(services, CommandNames.EmptyPlanCheck);
        AddBuilder<ApprovalContextBuilder>(services, CommandNames.Approval);
        AddBuilder<AgenticExecuteContextBuilder>(services, CommandNames.AgenticExecute);
        AddBuilder<AgenticMasterContextBuilder>(services, CommandNames.AgenticMaster);
        AddBuilder<TestContextBuilder>(services, CommandNames.Test);
        AddBuilder<WriteRunResultContextBuilder>(services, CommandNames.WriteRunResult);
        AddBuilder<CommitAndPRContextBuilder>(services, CommandNames.CommitAndPR);
        AddBuilder<InitCommitContextBuilder>(services, CommandNames.InitCommit);
        AddBuilder<PrCrossLinkContextBuilder>(services, CommandNames.PrCrossLink);
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
        AddBuilder<BootstrapDiscoverContextBuilder>(services, CommandNames.BootstrapDiscover);
        AddBuilder<BootstrapRoundContextBuilder>(services, CommandNames.BootstrapRound);
        return services;
    }

    private static void AddBuilder<TBuilder>(IServiceCollection services, string commandName)
        where TBuilder : IContextBuilder, new()
        => services.AddSingleton(new KeyedContextBuilder(commandName, new TBuilder()));
}
