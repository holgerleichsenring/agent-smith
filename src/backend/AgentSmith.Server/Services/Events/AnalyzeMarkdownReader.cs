using AgentSmith.Application.Services;
using AgentSmith.Contracts.Persistence;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0243: reads the cached analyze.md for a run from the artifact store. Mirrors
/// <see cref="PlanMarkdownReader"/> / <see cref="ResultMarkdownReader"/> — the
/// analyzer's ProjectMap rendered as markdown, cached by AnalyzeProjectHandler
/// right after the Analyze step (24h TTL). Null when the run is unknown, the
/// cache has expired, or no analysis was cached; the dashboard hides the panel.
/// </summary>
public sealed class AnalyzeMarkdownReader(IRunArtifactStore store)
{
    public async Task<string?> ReadAsync(string runId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(runId)) return null;
        if (!RunIdGenerator.IsValid(runId)) return null;
        return await store.ReadAnalyzeMarkdownAsync(runId, ct);
    }
}
