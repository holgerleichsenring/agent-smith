using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0348: the pods a run ACTUALLY spawned, read from the persisted RunSandbox
/// rows (the SandboxCreated/Disposed event stream) — the honest answer to the
/// side rail's "N pods · XGi", which operators read as live compute. Distinct
/// from <see cref="RunFootprintView"/>, the admission RESERVATION (every
/// configured repo + a synthetic orchestrator pod, summed at limits) that
/// over-counts what actually runs. Null until the first sandbox lands, so the
/// client renders "calculating…" instead of a fabricated count; the rows persist,
/// so the value stays correct after the run finishes.
/// </summary>
public sealed record RunComputeView(IReadOnlyList<RunComputePod> Pods, string TotalMem)
{
    private const double BytesPerGi = 1024d * 1024d * 1024d;

    public static RunComputeView? From(ICollection<RunSandbox> sandboxes)
    {
        if (sandboxes.Count == 0) return null;

        var pods = sandboxes
            .OrderBy(s => s.RepoName ?? s.Key, StringComparer.Ordinal)
            .Select(s => new RunComputePod(
                s.RepoName ?? s.Key,
                s.ToolchainImage ?? "—",
                s.MemoryRequest ?? "—",
                s.Status ?? "created"))
            .ToList();

        long totalBytes = 0;
        var anyMem = false;
        foreach (var s in sandboxes)
            if (s.MemoryRequest is { } req && KubernetesQuantity.TryParseMemoryToBytes(req, out var bytes))
            {
                totalBytes += bytes;
                anyMem = true;
            }
        var totalMem = anyMem ? (totalBytes / BytesPerGi).ToString("0.#") + "Gi" : "—";

        return new RunComputeView(pods, totalMem);
    }
}

/// <summary>p0348: one actually-spawned sandbox pod on the run-detail compute view.</summary>
public sealed record RunComputePod(string Repo, string Image, string Mem, string Status);
