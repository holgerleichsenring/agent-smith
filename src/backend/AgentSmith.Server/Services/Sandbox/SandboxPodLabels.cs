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
    public const string PipelineIdLabel = "pipeline-id";
    public const string RunIdLabel = "run-id";
    public const string OwnerLabel = "owner";

    /// <summary>
    /// 2026-09-22-2d11a: the design conversation a source-scope pod belongs to. A
    /// session id is eight hex characters, so it is a legal label value by shape.
    /// </summary>
    public const string ConversationIdLabel = "conversation-id";

    /// <summary>Pods stamped by this liveness store's server — the reaper's candidates.</summary>
    public string OwnedSelector => $"app={AppLabel},{OwnerLabel}={owner.Value}";

    /// <summary>Pods from a binary that predates the owner stamp — the one-time sweep.</summary>
    public const string UnownedSelector = $"app={AppLabel},!{OwnerLabel}";

    public Dictionary<string, string> Build(string jobId, string? runId, string? conversationId = null)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["app"] = AppLabel,
            [PipelineIdLabel] = jobId,
            [OwnerLabel] = owner.Value
        };
        // p0355: stamp the owning run so the corpse reaper can map pod -> run. Empty
        // when the sandbox is built outside a pipeline run (probe/preflight).
        if (!string.IsNullOrEmpty(runId)) labels[RunIdLabel] = runId;
        // 2026-09-22-2d11a: and the conversation, so the corpse sweep can tell a pod a
        // design turn may come back to from one nobody is coming back to.
        if (!string.IsNullOrEmpty(conversationId)) labels[ConversationIdLabel] = conversationId;
        return labels;
    }
}
