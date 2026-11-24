namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933dd: what a poc-deploy run's build step writes on a revision — the host pipeline run it follows (stored
/// before the first read, cleared for a fresh start), an image-stage block, and the pushed digest. An image block blocks
/// a PoC that never reached ready (importing or blocked); a built revision turns a blocked PoC back to importing; a
/// ready PoC keeps serving.
/// </summary>
public interface IPocBuildStore
{
    /// <summary>Stores the pipeline run, or clears it with both null.</summary>
    Task RecordPipelineRunAsync(long pocId, int revision, string? runId, string? url, CancellationToken ct);

    /// <summary>Blocks the revision at the image stage with <paramref name="verdictJson"/>.</summary>
    Task RecordImageBlockAsync(long pocId, int revision, string verdictJson, CancellationToken ct);

    /// <summary>Records the revision built with its confirmed <paramref name="digest"/> and the image scan's notes.</summary>
    Task RecordBuiltAsync(long pocId, int revision, string digest, string verdictJson, CancellationToken ct);
}
