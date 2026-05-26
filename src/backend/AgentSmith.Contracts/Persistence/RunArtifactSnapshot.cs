namespace AgentSmith.Contracts.Persistence;

/// <summary>
/// Snapshot of in-flight run artifacts a <see cref="IRunArtifactStore"/> holds for
/// a run id. Returned by Promote so the caller (typically WriteRunResultHandler)
/// can write the payloads to durable storage before the store clears its keys.
/// Each property is null when the corresponding artifact was never written.
/// </summary>
public sealed record RunArtifactSnapshot(string? PlanJson, string? DiffJson, string? BootstrapMarkdown)
{
    public static RunArtifactSnapshot Empty { get; } = new(null, null, null);

    public bool IsEmpty
        => PlanJson is null && DiffJson is null && BootstrapMarkdown is null;
}
