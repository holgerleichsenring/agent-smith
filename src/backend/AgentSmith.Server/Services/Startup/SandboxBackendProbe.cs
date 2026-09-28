using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// Can the composed sandbox backend create sandboxes? Every run's work happens in one, so an
/// unreachable backend is a blocking finding that names the backend and the cause.
/// </summary>
public sealed class SandboxBackendProbe(IPreflightSandboxProbe backend) : IStartupProbe
{
    public string Subsystem => StartupSubsystems.SandboxBackend;

    public async Task<IReadOnlyList<StartupFinding>> ProbeAsync(CancellationToken cancellationToken)
    {
        var result = await backend.ProbeAsync(cancellationToken);
        if (result.Ok) return [];
        return [new StartupFinding(
            StartupSubsystems.SandboxBackend,
            StartupFindingSeverity.Blocking,
            $"The {backend.BackendLabel} sandbox backend is not reachable, so no run can create a "
            + $"sandbox. Cause: {result.Error ?? "not stated"}",
            Field: "SANDBOX_TYPE")];
    }
}
