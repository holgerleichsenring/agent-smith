using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// 2026-10-16-86e6b: a park a newer attempt of its ticket started past is ended as superseded. Any start
/// path can leave one behind — a claim from the tracker, a series start, the poller — so the outcome they
/// share is read here instead of at each of them: the park stops holding capacity and its question can no
/// longer resume an obsolete run beside the newer one. Runs first in the resume sweep, under its leader.
/// </summary>
public sealed class SupersededParkCloser(
    IServiceScopeFactory scopeFactory,
    IWaitingRunCloser closer,
    IRunListNudge runListNudge,
    ILogger<SupersededParkCloser> logger)
{
    /// <summary>Closes every superseded park; returns how many.</summary>
    public async Task<int> CloseAsync(CancellationToken cancellationToken)
    {
        var parks = await ListAsync(cancellationToken);
        foreach (var park in parks)
        {
            await closer.CloseSupersededByAsync(park.RunId, park.NewerRunId, cancellationToken);
            logger.LogInformation("Run {RunId} of ticket {Ticket} closed: superseded by attempt {Newer}",
                park.RunId, park.TicketId, park.NewerRunId);
            await NudgeAsync(park.RunId, cancellationToken);
        }
        return parks.Count;
    }

    private async Task<IReadOnlyList<SupersededPark>> ListAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ParkedRunRepository>().ListSupersededAsync(cancellationToken);
    }

    // A parked run is outside the set the broadcaster drains, so an open surface is told here. Best-effort.
    private async Task NudgeAsync(string runId, CancellationToken cancellationToken)
    {
        try { await runListNudge.RunsChangedAsync(runId, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Run list nudge failed for superseded run {RunId}", runId);
        }
    }
}
