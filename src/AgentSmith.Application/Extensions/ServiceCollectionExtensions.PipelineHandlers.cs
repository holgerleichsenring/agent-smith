using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Pipeline;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application;

public static partial class ServiceCollectionExtensions
{
    // Pipeline-step handlers: the basic command handlers driven by PipelineExecutor.
    // p0125d: CheckoutSourceHandler / TryCheckoutSourceHandler also expose IConceptWriter —
    // three-step registration (concrete + interface + singleton-IConceptWriter) so the
    // validate-concepts registry sees them without changing the transient lifetime.
    private static void AddPipelineHandlers(IServiceCollection services)
    {
        services.AddTransient<ICommandHandler<FetchTicketContext>, FetchTicketHandler>();
        services.AddTransient<CheckoutSourceHandler>();
        services.AddTransient<ICommandHandler<CheckoutSourceContext>>(sp =>
            sp.GetRequiredService<CheckoutSourceHandler>());
        services.AddSingleton<IConceptWriter>(sp =>
            sp.GetRequiredService<CheckoutSourceHandler>());
        services.AddTransient<TryCheckoutSourceHandler>();
        services.AddTransient<ICommandHandler<TryCheckoutSourceContext>>(sp =>
            sp.GetRequiredService<TryCheckoutSourceHandler>());
        services.AddSingleton<IConceptWriter>(sp =>
            sp.GetRequiredService<TryCheckoutSourceHandler>());
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
        services.AddTransient<PromptPrefixBuilder>();
        services.AddTransient<ISkillPromptBuilder, SkillPromptBuilder>();
        services.AddTransient<IGateOutputHandler, GateOutputHandler>();
        services.AddTransient<IGateRetryCoordinator, GateRetryCoordinator>();
        services.AddTransient<IUpstreamContextBuilder, UpstreamContextBuilder>();
        services.AddTransient<ICommandHandler<SkillRoundContext>, SkillRoundHandler>();
        services.AddTransient<ICommandHandler<FilterRoundContext>, FilterRoundHandler>();
        // p0129a: Verify phase + p0128b: Plan open-questions round-trip.
        services.AddTransient<ICommandHandler<RunVerifyPhaseContext>, VerifyRoundHandler>();
        services.AddTransient<ICommandHandler<PersistWorkBranchContext>, PersistWorkBranchHandler>();
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
        services.AddTransient<ICommandHandler<AskContext>, AskCommandHandler>();
        services.AddTransient<ICommandHandler<CompileKnowledgeContext>, CompileKnowledgeHandler>();
        services.AddTransient<ICommandHandler<QueryKnowledgeContext>, QueryKnowledgeHandler>();
        services.AddTransient<ICommandHandler<LoadRunsContext>, LoadRunsHandler>();
        services.AddTransient<ICommandHandler<WriteTicketsContext>, WriteTicketsHandler>();
        services.AddSingleton<KnowledgePromptBuilder>();
        services.AddSingleton<StructuredOutputInstructionBuilder>();
    }
}
