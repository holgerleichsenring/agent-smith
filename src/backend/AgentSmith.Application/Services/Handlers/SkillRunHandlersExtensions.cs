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
        services.AddTransient<IConvergenceEvaluator, ConvergenceEvaluator>();
        services.AddTransient<ICommandHandler<BootstrapRoundContext>, BootstrapRoundHandler>();
        services.AddTransient<ICommandHandler<PlanOpenQuestionsContext>, PlanOpenQuestionsHandler>();
        services.AddSingleton<PlanAnswerParser>();
        services.AddSingleton<IPlanOpenQuestionsPoster, PlanOpenQuestionsPoster>();
        // p0391: the park status both clarification gates halt into — resolved, never optional.
        services.AddSingleton<IClarificationParkStatusResolver, ClarificationParkStatusResolver>();
        services.AddTransient<IPlanOpenQuestionExtractor, PlanOpenQuestionExtractor>();
        services.AddSingleton<IGitIgnoreResolver, NullGitIgnoreResolver>();
        services.AddSingleton<IPathReadGuard, PathReadGuard>();
        services.AddSingleton<IPathWriteGuard, PathWriteGuard>();
        services.AddTransient<BootstrapToolHostFactory>();
        // p0379: deterministic principles transfer (composed core+delta) that
        // runs inside the bootstrap round before the skill call.
        services.AddTransient<BootstrapPrinciplesTransfer>();
        return services;
    }
}
