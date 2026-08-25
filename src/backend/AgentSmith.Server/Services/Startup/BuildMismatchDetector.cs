using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Startup;

/// <summary>
/// 2026-08-25-8c97: compares the caller's build against this server's and says so as an
/// advisory finding. It says the builds are DIFFERENT and stops there — whether they can
/// talk to each other is a property of the contract between them, and that contract is not
/// generated from the server today.
/// </summary>
public sealed class BuildMismatchDetector(BuildIdentity server, TimeProvider time)
    : IBuildMismatchDetector
{
    /// <summary>
    /// How long a difference is treated as an upgrade in progress rather than a stale
    /// caller. Server and dashboard are separate Deployments, separately pinned, rolling
    /// two replicas each behind readiness probes that admit a pod ~15s after it starts —
    /// so a normal upgrade has both halves mixed for seconds to low minutes, and this
    /// window is a wide multiple of it. It is anchored on THIS process's start, the only
    /// rollout a process can observe: a pod that came up minutes ago is itself the new
    /// half, while one that has been serving for hours facing a caller from another build
    /// is facing a browser tab holding a bundle nobody replaced.
    /// </summary>
    public static readonly TimeSpan RolloutWindow = TimeSpan.FromMinutes(10);

    private readonly DateTimeOffset _startedAt = time.GetUtcNow();

    public IReadOnlyList<StartupFinding> Compare(string? callerRevision)
    {
        var caller = new BuildIdentity(callerRevision, null);
        if (!server.DiffersFrom(caller)) return [];
        if (time.GetUtcNow() - _startedAt < RolloutWindow) return [];
        return [new StartupFinding(
            StartupSubsystems.Build, StartupFindingSeverity.Advisory, Reason(caller))];
    }

    private string Reason(BuildIdentity caller) =>
        $"This page came from build {caller.ShortRevision}; the server is running "
        + $"{server.Display}. They are different builds — that is not by itself a fault, "
        + "and nothing has been refused. Reload to pick up the build this server serves.";
}
