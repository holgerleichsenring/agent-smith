using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0327: turns a pending checkpoint + its answer into a resume launch. The
/// resume goes through the existing capacity queue (IsResume entry reusing the
/// reserved run row) — resumed runs compete for capacity like new ones.
/// </summary>
public interface IRunResumer
{
    /// <summary>Enqueues the resume and marks the checkpoint consumed. Returns
    /// false when another writer already resumed it (idempotent).</summary>
    Task<bool> EnqueueResumeAsync(
        RunCheckpointRecord checkpoint, DialogAnswer answer, CancellationToken cancellationToken);
}
