using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// p0391a: the referential checks <see cref="AgentSmithConfigValidator"/> collects. They
/// used to be logged and then thrown as one InvalidOperationException out of Program —
/// the errors were named, and the process that named them died anyway. Each one is now a
/// blocking finding on the project and trigger it belongs to, so only that unit stops.
/// </summary>
public sealed class ConfigurationProbe(
    AgentSmithConfig config, AgentSmithConfigValidator validator) : IStartupProbe
{
    public string Subsystem => StartupSubsystems.Configuration;

    public Task<IReadOnlyList<StartupFinding>> ProbeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(validator.Findings(config));
}
