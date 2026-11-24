using AgentSmith.Application.Services.Pocs.Lifetime;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.Lifecycle;
using AgentSmith.Server.Services.Pocs.Acts;

namespace AgentSmith.Server.Services.Pocs.Lifetime;

/// <summary>
/// 2026-11-23-c81ee: one lifetime pass. It reads the live PoCs, decides per PoC in memory (<see cref="PocLifetimePlan"/>)
/// and starts c81ed's poc-stop or poc-delete through <see cref="PocActLauncher"/> with the lifetime case as a seed and the
/// actor 'poc-lifetime' — skipping a PoC whose last run is live or a failed stop or delete of the last day. Then, when the
/// latest poc-upload-purge run on '@agent-smith' did not finish within the last day, it runs one through IActRun. It writes
/// nothing itself: the runs do.
/// </summary>
internal sealed class PocLifetimePass(
    IPocLifetimeStore store, PocLifetimeRunRepository runs, PocRunGateRepository gate, PocRepository pocs, PocActLauncher launcher,
    PocLifetime lifetime, PocLifetimePlan plan, IActRun actRuns, TimeProvider clock, ILogger<PocLifetimePass> logger)
{
    public const string Actor = "poc-lifetime";

    private static readonly TimeSpan PurgeEvery = TimeSpan.FromDays(1); // not a limit: the spec's daily cadence of the upload purge

    /// <summary>Runs one pass; how many lifecycle runs it started.</summary>
    public async Task<int> RunAsync(PocsConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);
        var subjects = await store.LiveAsync(ct);
        var lastRuns = await runs.RunsAsync([.. subjects.Select(s => s.LastRunId).OfType<string>()], ct);
        var launched = 0;
        foreach (var poc in subjects)
            if (await LaunchAsync(poc, lastRuns, config, ct)) launched++;
        await PurgeAsync(ct);
        return launched;
    }

    private async Task<bool> LaunchAsync(
        PocLifetimeSubject poc, IReadOnlyDictionary<string, PocLastRun> lastRuns, PocsConfig config, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var state = lifetime.TargetOf(config, poc.Target) is { } target ? lifetime.Of(poc.Lifetime, target, now) : null;
        if (plan.CaseOf(poc, state, config.Lifetime, now) is not { } lifetimeCase) return false;
        var last = poc.LastRunId is { } id && lastRuns.TryGetValue(id, out var run) ? run : null;
        var live = last is { FinishedAt: null } && await gate.IsLiveAsync(last.Id, RunLivenessReaper.StaleAfter, ct);
        if (plan.Skips(last, live, now) || await pocs.FindAsync(poc.PocId, ct) is not { } row) return false;
        var (answer, refusal) = await launcher.LaunchAsync(row, PocLifetimeCases.ActOf(lifetimeCase), new PocActRequest(ConfirmName: row.Name),
            Actor, ct, lifetimeCase: lifetimeCase);
        if (answer is null) logger.LogInformation("PoC {PocId}: {Case} not started: {Why}", poc.PocId, lifetimeCase, refusal?.Reason);
        else logger.LogInformation("PoC {PocId}: {Case} started (run {RunId})", poc.PocId, lifetimeCase, answer.RunId);
        return answer is not null;
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        var latest = await runs.LatestAsync(InstallationScope.Name, PipelinePresets.PocUploadPurgeName, ct);
        if (latest is { FinishedAt: { } finished } && clock.GetUtcNow() - finished < PurgeEvery) return;
        if (latest is { FinishedAt: null } && await gate.IsLiveAsync(latest.Id, RunLivenessReaper.StaleAfter, ct)) return;
        var result = await actRuns.RunAsync(InstallationScope.Name, PipelinePresets.PocUploadPurgeName, new Dictionary<string, object>());
        logger.LogInformation("PoC upload purge: {Outcome}", result.Message);
    }
}
