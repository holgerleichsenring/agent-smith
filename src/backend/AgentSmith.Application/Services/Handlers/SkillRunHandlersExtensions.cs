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
/// security + API API-security round handlers, the open-questions round-trip, the
/// verify round, convergence-check, and the bootstrap producer-loop round. The
/// clarification poster + park resolver are Singleton — both are stateless and
/// re-used across handler instances.
/// </summary>
public static class SkillRunHandlersExtensions
{
    public static IServiceCollection AddSkillRunHandlers(this IServiceCollection services)
    {
        services.AddTransient<ICommandHandler<BootstrapRoundContext>, BootstrapRoundHandler>();
        services.AddSingleton<PlanAnswerParser>();
        services.AddSingleton<IPlanOpenQuestionsPoster, PlanOpenQuestionsPoster>();
        // p0457: the address a waiting ticket comment points at — configuration, so an
        // unconfigured deployment prints nothing rather than a guess.
        services.AddSingleton<Dialogue.RunAnswerLink>();
        // p0391: the park status the clarification gates halt into — resolved, never optional.
        services.AddSingleton<IClarificationParkStatusResolver, ClarificationParkStatusResolver>();
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
