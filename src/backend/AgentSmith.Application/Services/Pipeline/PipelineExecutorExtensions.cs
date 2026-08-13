using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Pipeline;

/// <summary>
/// p0405: the executor and the collaborators it owns — the parked/skipped
/// inspection (p0403), the finalizer tail that still has to run when a step
/// failed (p0237), and the planned-steps announcement the run detail reads as
/// "what is still coming". Registered together because the executor is the only
/// thing that resolves them.
/// </summary>
public static class PipelineExecutorExtensions
{
    public static IServiceCollection AddPipelineExecutor(this IServiceCollection services)
    {
        services.AddTransient<PipelineExecutor>();
        services.AddTransient<PipelineExecutorPolicy>();
        services.AddTransient<PipelineFinalizerTail>();
        services.AddTransient<PlannedStepsAnnouncer>();
        services.AddTransient<IPipelineExecutor>(sp => sp.GetRequiredService<PipelineExecutor>());
        return services;
    }
}
