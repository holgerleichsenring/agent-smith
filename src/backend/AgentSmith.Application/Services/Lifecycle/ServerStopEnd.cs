using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Hosting;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// 2026-11-22-6d85c: the end of a run the server's own stop broke off. The host's stopping token reaches the pipeline as a
/// cancellation, which no cancel arm owned (they are for a person's or a watchdog's cancel and an internal timeout), so the
/// run ended failed and read red. It is the infrastructure end the liveness reaper writes for a killed process instead —
/// cancelled with <see cref="OwedWorkCancel.Interrupted"/> — so Retry unchanged is offered, the claim door does not hold
/// the ticket and the interrupted restart retries it. Without a host (the CLI) nothing is a server stop.
/// </summary>
public sealed class ServerStopEnd(IEventPublisher events, TimeProvider clock, IHostApplicationLifetime? lifetime = null)
{
    public const string Summary = "Interrupted: the server stopped while this run was executing; the work is still owed.";

    /// <summary>Whether the server is stopping now.</summary>
    public bool Stopping => lifetime?.ApplicationStopping.IsCancellationRequested == true;

    /// <summary>Records the interrupted end of <paramref name="runId"/>; lands under a cancelled token.</summary>
    public async Task<CommandResult> EndAsync(string runId, decimal? costUsd)
    {
        var now = clock.GetUtcNow();
        await events.PublishAsync(new RunCancelRequestedEvent(runId, OwedWorkCancel.Interrupted, now), CancellationToken.None);
        await events.PublishAsync(new RunFinishedEvent(runId, LeftInPlaceEnd.Cancelled, null, Summary, now, costUsd), CancellationToken.None);
        return CommandResult.Fail(Summary);
    }
}
