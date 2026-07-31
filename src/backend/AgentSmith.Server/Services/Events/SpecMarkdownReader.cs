using AgentSmith.Application.Services;
using AgentSmith.Contracts.Persistence;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0390: reads the cached work spec for a run from the artifact store. Mirrors
/// <see cref="PlanMarkdownReader"/> — the content of record lives in git on the
/// ticket branch, and this slot is the run detail's copy, written when the
/// revision is committed. Null when the run derived no spec or the cache has
/// expired; the dashboard then shows only the plan.
/// </summary>
public sealed class SpecMarkdownReader(IRunArtifactStore store)
{
    public async Task<string?> ReadAsync(string runId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(runId)) return null;
        if (!RunIdGenerator.IsValid(runId)) return null;
        return await store.ReadSpecMarkdownAsync(runId, ct);
    }
}
