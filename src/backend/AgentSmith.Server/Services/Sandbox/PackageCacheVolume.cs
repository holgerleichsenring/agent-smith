using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0407: a <see cref="PackageCacheMount"/> as Docker provides it — a named volume
/// that survives every sandbox. The ecosystem-neutral cache says what it needs;
/// this says how this backend hands it over.
/// </summary>
public sealed record PackageCacheVolume(string VolumeName, PackageCacheMount Cache)
{
    /// <summary>Docker bind syntax — read-write, since filling the cache is the point.</summary>
    public string Bind => $"{VolumeName}:{Cache.MountPath}";

    /// <summary>The cache's env vars in Docker's <c>NAME=value</c> form.</summary>
    public IEnumerable<string> EnvAssignments => Cache.Env.Select(e => $"{e.Key}={e.Value}");
}
