using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services;

/// <summary>
/// DI registration for <see cref="ITolerantJsonParser"/> + its telemetry seam,
/// plus the LLM-output observation parsers that consume them.
/// Parsers are stateless → Transient; telemetry default is the no-op singleton,
/// overridable by hosting layers that wire a counter-emitting impl.
/// </summary>
public static class TolerantJsonParserExtensions
{
    public static IServiceCollection AddTolerantJsonParser(this IServiceCollection services)
    {
        services.AddTransient<ITolerantJsonParser, TolerantJsonParser>();
        services.AddTransient<IObservationNormalizer, ObservationNormalizer>();
        services.AddTransient<ObservationParser>();
        services.AddTransient<GateObservationParser>();
        services.AddTransient<PlanParser>();
        services.AddTransient<ConsolidationResponseParser>();
        services.AddTransient<ConvergenceResultParser>();
        services.AddTransient<WikiUpdateParser>();
        services.TryAddSingleton<ITolerantParseTelemetry, NoOpTolerantParseTelemetry>();
        return services;
    }

    /// <summary>
    /// Default no-op telemetry. Hosts that want counters replace this in their
    /// composition root by calling AddSingleton&lt;ITolerantParseTelemetry, …&gt;()
    /// after AddAgentSmithCommands.
    /// </summary>
    private sealed class NoOpTolerantParseTelemetry : ITolerantParseTelemetry
    {
        public void Record(TolerantRecoveryKind kind, string detail) { }
    }
}
