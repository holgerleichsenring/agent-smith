using AgentSmith.Application.Services.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-09-07-f420: what a run's outcome says about itself to the ticket author and the
/// reviewer — the completed summary, the failure comment, and the wording of a failed
/// run's persisted work. Registered together because they are the three voices one
/// run can speak with, and a failed run must never borrow the completed one.
/// </summary>
public static class RunOutcomeWordingExtensions
{
    public static IServiceCollection AddRunOutcomeWording(this IServiceCollection services)
    {
        services.AddTransient<CompletedRunTicketSummary>();
        services.AddTransient<FailedRunPersistence>();
        services.AddTransient<FailureTicketComment>();
        return services;
    }
}
