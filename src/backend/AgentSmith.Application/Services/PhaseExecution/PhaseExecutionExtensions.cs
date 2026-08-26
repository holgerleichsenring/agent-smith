using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// 2026-08-26-31e5: the phase-execution steps register themselves. They had accumulated in
/// the middle of the handler registry, which sits over the file-length ratchet — and the
/// index line needed room the registry did not have.
/// </summary>
public static class PhaseExecutionExtensions
{
    public static IServiceCollection AddPhaseExecution(this IServiceCollection services)
    {
        services.AddTransient<ICommandHandler<CommitPhaseWorkContext>, CommitPhaseWorkHandler>(); // p0437
        services.AddTransient<ExecutedPhaseMarker>(); // p0466
        // p0466's server copy, its own type since 2026-08-26-31e5.
        services.AddTransient<PhaseRecordPublisher>();
        // 2026-08-26-31e5: the state.done line that names the record file.
        services.AddTransient<PhaseRecordIndexLine>();
        services.AddTransient<PhaseIndexWriter>();
        services.AddTransient<ICommandHandler<WritePhaseRecordContext>, WritePhaseRecordHandler>();
        return services;
    }
}
