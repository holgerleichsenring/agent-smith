using AgentSmith.Application.Models;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Services.Rework;

/// <summary>2026-11-17-3435b: runs one merge-green run for a ticket; answers whether it ran (a held lease is a no).
/// 2026-11-23-31dce: with <paramref name="outcome"/> seeded, the run's merge step fills it with each repo's outcome.</summary>
public interface IMergeGreenRun
{
    Task<bool> RunAsync(ResolvedProject project, CiGreenMergeRequest request, CancellationToken ct,
        ActSlot<IReadOnlyList<PullRequestMergeLine>>? outcome = null);
}
