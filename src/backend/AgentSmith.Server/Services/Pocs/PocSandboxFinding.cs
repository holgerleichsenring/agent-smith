using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Pocs;

/// <summary>
/// 2026-11-23-933dc: while PoCs are on, a server on the in-process sandbox backend — which runs no image — is named on
/// the findings channel: every PoC upload's dependency scan needs a backend that runs the pinned scanner image, so each
/// poc-deploy run would fail there. Republished with the poc- prefix findings.
/// </summary>
internal sealed class PocSandboxFinding(SandboxContainerRuntime runtime, IStartupFindings findings)
{
    internal const string Subsystem = "pocs-sandbox";

    public void Report(AgentSmithConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        findings.Clear(Subsystem);
        if (!config.Pocs.Enabled || runtime.SpawnsContainers) return;
        findings.Record(new StartupFinding(Subsystem, StartupFindingSeverity.Advisory,
            "PoCs are enabled, but this server runs sandboxes in process, which runs no image: every PoC upload's dependency "
            + "scan needs Kubernetes or Docker, so each poc-deploy run fails until one is available.", Field: "pocs"));
    }
}
