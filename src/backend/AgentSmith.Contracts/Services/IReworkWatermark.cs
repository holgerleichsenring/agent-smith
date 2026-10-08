namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-08-0781: an operator stopping a run — a cancel or a delete — withholds every rework act
/// on its ticket made up to that moment. A run without a ticket, or not of the code pipeline, has none.
/// </summary>
public interface IReworkWatermark
{
    Task WithholdRunAsync(string runId, DateTimeOffset at, CancellationToken cancellationToken);
}
