using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// Writes the waiting row's terminal through <see cref="CancelTerminalWriter"/> — a paused run left
/// the active set, so a published event alone would never reach its row. The writer goes through the
/// finalization projection, which releases the capacity the pause kept; its set-once guard makes a
/// second close a no-op.
/// </summary>
public sealed class WaitingRunCloser(CancelTerminalWriter terminalWriter, TimeProvider clock) : IWaitingRunCloser
{
    public const string Summary = "cancelled — superseded by a newer act";

    public Task CloseSupersededAsync(string runId, CancellationToken cancellationToken) =>
        CloseAsync(runId, Summary, cancellationToken);

    public Task CloseSupersededByAsync(string runId, string newerRunId, CancellationToken cancellationToken) =>
        CloseAsync(runId, $"cancelled — superseded by attempt {newerRunId}", cancellationToken);

    private Task CloseAsync(string runId, string summary, CancellationToken cancellationToken) =>
        terminalWriter.FinalizeAsync(new RunFinishedEvent(
            runId, "cancelled", null, summary, clock.GetUtcNow(), EndKind: RunEndKinds.Superseded), cancellationToken);
}
