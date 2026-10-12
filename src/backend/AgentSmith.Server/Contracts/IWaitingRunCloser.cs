namespace AgentSmith.Server.Contracts;

/// <summary>
/// Ends a run that waits on a hand-back because a pull-request act answered it. The row becomes
/// 'cancelled' and releases what the pause kept; the ticket is not moved and no rework watermark is
/// raised — the act that superseded it is the one the next attempt serves.
/// </summary>
public interface IWaitingRunCloser
{
    Task CloseSupersededAsync(string runId, CancellationToken cancellationToken);
}
