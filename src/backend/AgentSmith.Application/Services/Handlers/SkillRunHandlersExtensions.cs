using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Skill-round handlers: discussion / structured / filter SkillRound variants, the
/// security + API API-security round handlers, plan generation + open-questions
/// round-trip, the verify round, convergence-check, and the bootstrap producer-loop
/// round. PlanOpenQuestions registers its supporting parser + poster (Singleton —
/// both are stateless and re-used across handler instances).
/// </summary>
public static class SkillRunHandlersExtensions
{
    public static IServiceCollection AddSkillRunHandlers(this IServiceCollection services)
    {
        services.AddTransient<ICommandHandler<GeneratePlanContext>, GeneratePlanHandler>();
        services.AddTransient<ICommandHandler<SkillRoundContext>, SkillRoundHandler>();
        services.AddTransient<ICommandHandler<SecuritySkillRoundContext>, SecuritySkillRoundHandler>();
        services.AddTransient<ICommandHandler<PrReviewSkillRoundContext>, PrReviewSkillRoundHandler>();
        services.AddTransient<ICommandHandler<ApiSecuritySkillRoundContext>, ApiSkillRoundHandler>();
        services.AddTransient<ICommandHandler<FilterRoundContext>, FilterRoundHandler>();
        services.AddTransient<ICommandHandler<RunVerifyPhaseContext>, VerifyRoundHandler>();
        services.AddTransient<IConvergenceEvaluator, ConvergenceEvaluator>();
        services.AddTransient<ICommandHandler<ConvergenceCheckContext>, ConvergenceCheckHandler>();
        services.AddTransient<ICommandHandler<BootstrapRoundContext>, BootstrapRoundHandler>();
        services.AddTransient<ICommandHandler<PlanOpenQuestionsContext>, PlanOpenQuestionsHandler>();
        services.AddSingleton<PlanAnswerParser>();
        services.AddSingleton<IPlanOpenQuestionsPoster, PlanOpenQuestionsPoster>();
        services.AddTransient<PlanConsolidator>();
        services.AddTransient<IFilterRoundBatcher, FilterRoundBatcher>();
        services.AddTransient<FilterRoundCaller>();
        services.AddTransient<IVerifyRoundCoordinator, VerifyRoundCoordinator>();
        services.AddTransient<IPlanOpenQuestionExtractor, PlanOpenQuestionExtractor>();
        services.AddSingleton<IGitIgnoreResolver, NullGitIgnoreResolver>();
        services.AddSingleton<IPathReadGuard, PathReadGuard>();
        services.AddSingleton<IPathWriteGuard, PathWriteGuard>();
        services.AddTransient<BootstrapToolHostFactory>();
        return services;
    }
}
