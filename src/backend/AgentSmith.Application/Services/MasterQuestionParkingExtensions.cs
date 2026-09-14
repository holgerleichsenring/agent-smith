using AgentSmith.Application.Services.Triage;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-09-03-3c07: the two halves of a master's mid-run question — the checkpoint that
/// makes it answerable where it is shown (p0453), and the intake that hands the answer
/// back to the master when the run resumes. Registered together because a park without
/// its resume is the defect this phase closed.
/// </summary>
public static class MasterQuestionParkingExtensions
{
    public static IServiceCollection AddMasterQuestionParking(this IServiceCollection services)
    {
        services.AddTransient<MasterQuestionCheckpoint>();
        services.AddTransient<IMasterAnswerIntake, MasterAnswerIntake>();
        return services;
    }
}
