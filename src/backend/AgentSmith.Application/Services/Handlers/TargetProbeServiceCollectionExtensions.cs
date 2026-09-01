using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-01-379a: the target-probe step and the two services it is composed of — the
/// resolver that reads what each context declared, and the runner that asks one target and
/// returns an exit code and nothing else.
/// <para>
/// Registered from its own extension rather than as three more lines in
/// PipelineHandlersExtensions, which is at its length baseline: a file already over the
/// limit may only get shorter.
/// </para>
/// </summary>
public static class TargetProbeServiceCollectionExtensions
{
    public static IServiceCollection AddTargetProbe(this IServiceCollection services)
    {
        services.AddSingleton<ContextTargetProbeResolver>();
        services.AddTransient<TargetProbeRunner>();
        services.AddTransient<ICommandHandler<ProbeTargetContext>, ProbeTargetHandler>();
        return services;
    }
}
