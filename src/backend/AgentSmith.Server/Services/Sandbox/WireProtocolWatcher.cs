using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Sandbox.Wire;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-08-25-0d01: the reader the wire's schema version never had. All three wire records
/// have carried one since the protocol was written, stamped at about forty construction
/// sites and read at none — a declaration nobody ever checked. This is the check, on the
/// only side of the seam that has somewhere to publish it.
/// <para>
/// ADVISORY, never blocking, for the same reason 8c97's build mismatch is: an agent whose
/// tag was pinned to an older release is a running run, and refusing its results would
/// convert a difference into the failure the difference had not caused. The message is
/// still delivered; the operator is told what they are looking at.
/// </para>
/// <para>
/// Reported once and not cleared. Per-project pins mean one installation can legitimately
/// run agents of several releases at once, so clearing on the next in-window message would
/// make the banner flap between two sandboxes rather than describe the installation. A run
/// that already spoke to an unreadable agent is a fact, and a later good message does not
/// un-happen it.
/// </para>
/// </summary>
public sealed class WireProtocolWatcher(IStartupFindings findings, ILogger<WireProtocolWatcher> logger)
    : IWireProtocolWatcher
{
    private int _reported;

    public void Observe(int schemaVersion)
    {
        if (WireProtocol.IsSupported(schemaVersion)) return;
        if (Interlocked.Exchange(ref _reported, 1) == 1) return;
        logger.LogWarning(
            "A sandbox agent answered on wire protocol {Observed}; this server speaks {Window}",
            schemaVersion, WireProtocol.Window);
        findings.Record(new StartupFinding(
            StartupSubsystems.SandboxAgent, StartupFindingSeverity.Advisory, Reason(schemaVersion),
            Field: "schema_version"));
    }

    private static string Reason(int schemaVersion) =>
        $"A sandbox agent answered on wire protocol version {schemaVersion}, and this server "
        + $"speaks {WireProtocol.Window}. That is a sandbox-agent image from outside the range "
        + "this build was written against — the usual cause is a pinned 'sandbox.agent_version' "
        + "nobody moved. Nothing was refused over it; results are still being read.";
}
