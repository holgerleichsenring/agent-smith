namespace AgentSmith.Server.Contracts;

/// <summary>
/// Ends a run that waits on a hand-back because a pull-request act answered it. The row becomes
/// 'cancelled' and releases what the pause kept; the ticket is not moved and no rework watermark is
/// raised — the act that superseded it is the one the next attempt serves.
/// <para>2026-10-16-86e6b: both closes record the end kind superseded.</para>
/// </summary>
public interface IWaitingRunCloser
{
    Task CloseSupersededAsync(string runId, CancellationToken cancellationToken);

    /// <summary>2026-10-16-86e6b: ends a park a newer attempt of its ticket has started past, naming that attempt.</summary>
    Task CloseSupersededByAsync(string runId, string newerRunId, CancellationToken cancellationToken);
}
