namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0465: the label set a sandbox pod carries, and the selectors that read it back.
/// Stamp and selector live together so the corpse reaper cannot ask for a term the
/// spec builder never wrote. Bare (unprefixed) keys, matching the existing pod labels.
///
/// The owner is a LABEL rather than <c>metadata.ownerReferences</c> because the k8s
/// API cannot select on an owner reference: the list call would stay namespace-wide
/// and ownership would sink back into the decision function, which is what let a
/// foreign server's pods become candidates in the first place. An owner reference
/// also names the OWNING REPLICA — a per-process identity, and its garbage collection
/// would delete a live sandbox the moment its replica restarted.
/// </summary>
public sealed class SandboxPodLabels(SandboxOwnerIdentity owner)
{
    public const string AppLabel = "agentsmith-sandbox";
    public const string RunIdLabel = "run-id";
    public const string OwnerLabel = "owner";

    /// <summary>Pods stamped by this liveness store's server — the reaper's candidates.</summary>
    public string OwnedSelector => $"app={AppLabel},{OwnerLabel}={owner.Value}";

    /// <summary>Pods from a binary that predates the owner stamp — the one-time sweep.</summary>
    public const string UnownedSelector = $"app={AppLabel},!{OwnerLabel}";

    public Dictionary<string, string> Build(string jobId, string? runId)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["app"] = AppLabel,
            ["pipeline-id"] = jobId,
            [OwnerLabel] = owner.Value
        };
        // p0355: stamp the owning run so the corpse reaper can map pod -> run. Empty
        // when the sandbox is built outside a pipeline run (probe/preflight).
        if (!string.IsNullOrEmpty(runId)) labels[RunIdLabel] = runId;
        return labels;
    }
}
