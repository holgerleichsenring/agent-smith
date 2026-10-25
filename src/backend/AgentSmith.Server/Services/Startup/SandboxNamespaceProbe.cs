using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Sandbox;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// 2026-10-25-c5a7c: sandbox pods in a namespace of their own need a Redis URL they can resolve.
/// Without SANDBOX_REDIS_URL no sandbox can reach the job bus, so it is a blocking finding.
/// </summary>
public sealed class SandboxNamespaceProbe(KubernetesSandboxOptions options) : IStartupProbe
{
    public string Subsystem => StartupSubsystems.SandboxBackend;

    public Task<IReadOnlyList<StartupFinding>> ProbeAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StartupFinding>>(options.Misconfiguration is { } message
            ? [new StartupFinding(StartupSubsystems.SandboxBackend, StartupFindingSeverity.Blocking, message,
                Field: "SANDBOX_REDIS_URL")]
            : []);
}
