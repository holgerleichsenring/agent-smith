using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Services.Rework;

/// <summary>2026-11-17-3435b: runs one merge-green run for a ticket; answers whether it ran (a held lease is a no).</summary>
public interface IMergeGreenRun
{
    Task<bool> RunAsync(ResolvedProject project, CiGreenMergeRequest request, CancellationToken ct);
}
