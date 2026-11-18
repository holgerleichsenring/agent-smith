using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Lifecycle;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-17-0207a: on the housekeeping leader, at the resume sweep's interval, starts a beat for every active series
/// that needs one. A series runs one beat at a time: while its newest beat's run row is unfinished it gets none, and a
/// beat that died is finished by the run reaper, which frees the series — no lease of its own.
/// </summary>
public sealed class SeriesRunSweeper(IServiceScopeFactory scopes, TimeProvider clock, ILogger<SeriesRunSweeper> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await ScanOnceAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Series run scan failed"); }

            try { await Task.Delay(DialogueResumeSweeper.ScanInterval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Public so tests drive single scans. Answers the beats started.</summary>
    public async Task<int> ScanOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ISeriesRunStore>();
        var started = 0;
        foreach (var record in await store.ActiveAsync(ct))
        {
            try { started += await BeatIfDueAsync(scope.ServiceProvider, store, record, ct) ? 1 : 0; }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Series {Series} could not be beaten this scan", record.Id);
            }
        }
        return started;
    }

    private async Task<bool> BeatIfDueAsync(IServiceProvider services, ISeriesRunStore store, SeriesRunRecord record, CancellationToken ct)
    {
        if (await UnfinishedAsync(services, record.BeatRunId, ct)) return false;
        var facts = await services.GetRequiredService<ISeriesTicketFacts>().ReadAsync(record.Project, record.Tickets, ct);
        if (await services.GetRequiredService<SeriesBeatDemand>().WhyAsync(record, facts, clock.GetUtcNow(), ct) is not { } why)
            return false;
        var runId = await services.GetRequiredService<ISeriesRunLaunch>()
            .LaunchAsync(record.Project, PipelinePresets.SeriesRunName, record.Id, ct);
        await store.UpdateAsync(record.Id, r => r with { BeatRunId = runId }, ct);
        logger.LogInformation("Series {Series}: beat {RunId} started because {Why}", record.Id, runId, why);
        return true;
    }

    private static async Task<bool> UnfinishedAsync(IServiceProvider services, string? runId, CancellationToken ct) =>
        runId is not null && await services.GetRequiredService<RunRepository>().GetRunDetailAsync(runId, ct) is { FinishedAt: null };
}
