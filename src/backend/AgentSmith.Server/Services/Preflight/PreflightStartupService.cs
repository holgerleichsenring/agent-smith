using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Preflight;

/// <summary>
/// p0324: runs the same preflight checks the CLI `doctor` command runs, once at
/// server startup, WARN-ONLY — a failed check is logged with its fix hint and
/// surfaced on /health, but never stops the host (the operator decides; an assistant
/// that refuses to start over a degraded dependency is its own outage).
/// </summary>
internal sealed class PreflightStartupService(
    IPreflightRunner runner,
    PreflightReportStore store,
    ILogger<PreflightStartupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield immediately so host startup never waits on the first (possibly
        // synchronous) check — the preflight is observability, not a gate.
        await Task.Yield();
        try
        {
            var report = await runner.RunAsync(stoppingToken);
            store.Publish(report);
            foreach (var outcome in report.Outcomes)
                LogOutcome(outcome);
            logger.LogInformation(
                "Startup preflight: {Passed} passed, {Failed} failed, {Skipped} skipped",
                report.PassedCount, report.FailedCount, report.SkippedCount);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "Startup preflight cancelled during shutdown");
        }
    }

    private void LogOutcome(PreflightCheckOutcome outcome)
    {
        switch (outcome.Result.Status)
        {
            case PreflightStatus.Fail:
                logger.LogWarning(
                    "Preflight {Check} FAILED: {Message} — fix: {FixHint}",
                    outcome.Name, outcome.Result.Message, outcome.Result.FixHint);
                break;
            case PreflightStatus.Skip:
                logger.LogInformation(
                    "Preflight {Check} skipped: {Message}", outcome.Name, outcome.Result.Message);
                break;
            default:
                logger.LogInformation(
                    "Preflight {Check} ok: {Message} ({DurationMs} ms)",
                    outcome.Name, outcome.Result.Message, outcome.DurationMs);
                break;
        }
    }
}
