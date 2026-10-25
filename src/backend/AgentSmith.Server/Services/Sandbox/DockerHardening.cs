using AgentSmith.Contracts.Sandbox;
using Docker.DotNet.Models;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-25-c5a7b: what a sandbox's security profile adds to its Docker containers, the
/// Kubernetes profile's twin. Hardened: the toolchain runs as uid:gid with every capability
/// dropped, HOME and the safe.directory env of the profile, /root on its own volume. Root: the
/// image's user and default capabilities. Every container of a confined profile, the loader
/// included, runs with no-new-privileges; the loader also drops every capability, it writes only
/// to a volume it owns. Unconfined (2026-10-25-ff3b, hardening off): the containers as they were.
/// </summary>
public sealed class DockerHardening
{
    private const string NoNewPrivileges = "no-new-privileges";
    private const string NoCopy = ":nocopy";

    public string? User(SandboxSecurityContext profile) =>
        profile.RunAsRoot ? null : $"{profile.Uid}:{profile.Gid}";

    public IEnumerable<string> ToolchainEnv(SandboxSecurityContext profile) =>
        profile.ToolchainEnv().Select(kv => $"{kv.Key}={kv.Value}");

    /// <summary>
    /// The work bind, and the home bind when the set has one. Both carry nocopy in the hardened
    /// profile, so Docker's copy-up of an image's directory cannot reset the ownership given.
    /// </summary>
    public IEnumerable<string> VolumeBinds(DockerSandboxVolumeSet volumes, string workMount)
    {
        var suffix = volumes.Home is null ? string.Empty : NoCopy;
        yield return $"{volumes.Work}:{workMount}{suffix}";
        if (volumes.Home is not null) yield return $"{volumes.Home}:{SandboxSecurityContext.Home}{NoCopy}";
    }

    public void HardenToolchain(HostConfig host, SandboxSecurityContext profile)
    {
        if (!profile.Confined) return;
        host.SecurityOpt = [NoNewPrivileges];
        if (!profile.RunAsRoot) host.CapDrop = ["ALL"];
    }

    public void HardenLoader(HostConfig host, SandboxSecurityContext profile)
    {
        if (!profile.Confined) return;
        host.SecurityOpt = [NoNewPrivileges];
        host.CapDrop = ["ALL"];
    }
}
