using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// p0391a: the referential checks <see cref="AgentSmithConfigValidator"/> collects. They
/// used to be logged and then thrown as one InvalidOperationException out of Program —
/// the errors were named, and the process that named them died anyway. Each one is now a
/// blocking finding on the project and trigger it belongs to, so only that unit stops.
/// <para>
/// 2026-09-25-3c7ad: plus the ADVISORY check on the pipeline names routing rules carry, which is
/// its own collaborator because its severity is the opposite — a blocking finding would disable
/// every trigger on a project over a typo in one label rule.
/// </para>
/// </summary>
public sealed class ConfigurationProbe(
    AgentSmithConfig config, AgentSmithConfigValidator validator) : IStartupProbe
{
    public string Subsystem => StartupSubsystems.Configuration;

    public Task<IReadOnlyList<StartupFinding>> ProbeAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StartupFinding>>(
            [.. validator.Findings(config), .. RoutingPipelineNames.Findings(config)]);
}
