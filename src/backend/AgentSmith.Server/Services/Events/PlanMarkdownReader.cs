using AgentSmith.Application.Services;
using AgentSmith.Contracts.Persistence;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0235: reads the cached plan.md for a run from the artifact store. Mirrors
/// <see cref="ResultMarkdownReader"/> — for coding presets the plan is the
/// agent's own &lt;repo&gt;/.agentsmith/plan.md, cached by WriteRunResultHandler
/// at run-finish (24h TTL). Null when the run is unknown, the cache has expired,
/// or the agent wrote no plan; the dashboard simply hides the plan panel.
/// </summary>
public sealed class PlanMarkdownReader(IRunArtifactStore store)
{
    public async Task<string?> ReadAsync(string runId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(runId)) return null;
        if (!RunIdGenerator.IsValid(runId)) return null;
        return await store.ReadPlanMarkdownAsync(runId, ct);
    }
}
