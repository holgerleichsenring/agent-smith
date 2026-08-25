using Docker.DotNet.Models;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0465: builds the Docker list filters that scope a daemon-wide query to THIS
/// liveness store's sandboxes. Both readers of the daemon go through it — the orphan
/// reaper and the capacity probe — because an unscoped probe starves a second server
/// exactly as an unscoped reaper kills it.
/// </summary>
public sealed class DockerSandboxQuery(SandboxOwnerIdentity owner)
{
    /// <summary>Sandboxes stamped with this store's owner id.</summary>
    public ContainersListParameters Owned(bool includeStopped) =>
        Filtered(includeStopped, $"{DockerContainerSpecBuilder.OwnerLabel}={owner.Value}");

    /// <summary>
    /// Every sandbox on the daemon, whoever spawned it. Only the one-time sweep for
    /// pre-p0465 containers uses this — a container from an older binary carries no
    /// owner label, so it can never appear in an owned query.
    /// </summary>
    public ContainersListParameters AnyOwner(bool includeStopped) => Filtered(includeStopped);

    public static bool IsUnowned(ContainerListResponse container) =>
        container.Labels is null
        || !container.Labels.TryGetValue(DockerContainerSpecBuilder.OwnerLabel, out var value)
        || string.IsNullOrEmpty(value);

    private static ContainersListParameters Filtered(bool includeStopped, params string[] extraLabelTerms)
    {
        var labels = new Dictionary<string, bool> { [DockerContainerSpecBuilder.JobIdLabel] = true };
        foreach (var term in extraLabelTerms) labels[term] = true;
        return new ContainersListParameters
        {
            All = includeStopped,
            Filters = new Dictionary<string, IDictionary<string, bool>> { ["label"] = labels }
        };
    }
}
