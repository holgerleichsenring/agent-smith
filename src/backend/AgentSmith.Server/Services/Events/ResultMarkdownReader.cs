using AgentSmith.Application.Services;
using AgentSmith.Contracts.Persistence;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0169j-c: reads the cached result.md for a run from the artifact store.
/// The on-disk write (inside the sandbox / target repo) is not reachable
/// from the server; WriteRunResultHandler additionally caches the rendered
/// content into the artifact store on every run-finish. 24h TTL — older
/// runs fall back to the PR URL (the durable surface).
/// </summary>
public sealed class ResultMarkdownReader(IRunArtifactStore store)
{
    public async Task<string?> ReadAsync(string runId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(runId)) return null;
        if (!RunIdGenerator.IsValid(runId)) return null;
        return await store.ReadResultMarkdownAsync(runId, ct);
    }
}
