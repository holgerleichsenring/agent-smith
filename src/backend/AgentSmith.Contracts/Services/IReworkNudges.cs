using AgentSmith.Contracts.Runs;

namespace AgentSmith.Contracts.Services;

/// <summary>2026-10-08-0781: the durable queue of "check this ticket" the rework worker drains.</summary>
public interface IReworkNudges
{
    Task EnqueueAsync(ReworkNudgeRequest request, CancellationToken cancellationToken);
}
