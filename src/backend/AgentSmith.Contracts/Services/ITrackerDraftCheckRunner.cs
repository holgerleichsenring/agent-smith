using AgentSmith.Contracts.Models.ConfigStudio;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-02-5f89b: tests an unsaved tracker the way saving would store it, step by step,
/// stopping at the first failure.
/// </summary>
public interface ITrackerDraftCheckRunner
{
    Task<DraftCheckReport> RunAsync(TrackerEntity draft, CancellationToken cancellationToken);
}
