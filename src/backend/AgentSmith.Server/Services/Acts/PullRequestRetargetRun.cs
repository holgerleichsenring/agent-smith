using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Server.Services.Acts;

/// <summary>
/// 2026-11-17-9a39c: moves a stacked dependent's pull requests through a 'retarget' run — the server chose the moves; the
/// run's step makes them under a run id. Answers how many moved, or null with how the run ended when it ended first.
/// </summary>
public sealed class PullRequestRetargetRun(IActRun runs)
{
    public async Task<(int? Moved, string? Ended)> RunAsync(ResolvedProject project, string ticketId, IReadOnlyList<PullRequestMove> moves)
    {
        ArgumentNullException.ThrowIfNull(project);
        var slot = new ActSlot<int>();
        var result = await runs.RunAsync(project.Name, PipelinePresets.RetargetName, new Dictionary<string, object>
        {
            [ContextKeys.RetargetPullRequestsRequest] = new RetargetPullRequestsRequest(ticketId, moves),
            [ContextKeys.ActOutcome] = slot,
        }, new TicketId(ticketId)); // 2026-11-17-b34ed: listed on the dependent's page
        return slot.IsSet ? (slot.Value, null) : (null, result.Message ?? "the retarget run ended before its step");
    }
}
