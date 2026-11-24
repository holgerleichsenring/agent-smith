using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Pocs.Rollout;

/// <summary>
/// 2026-11-23-c81ec: refreshes the PoC's Application once, then reads it and its live Deployment every
/// pocs.rollout.poll_seconds and judges each reading, one progress line per poll, until it settles, fails or the budget
/// ends. An unreachable Argo CD or a 5xx is a waiting reading; any other refusal ends the follow. Idempotent as a read: a
/// retried follow settles on its first poll when the cluster already holds the state.
/// </summary>
public sealed class PocRolloutWatch(PocRolloutVerdict verdict, PocRolloutLines lines, TimeProvider clock, ILogger<PocRolloutWatch> logger)
{
    public async Task<(PocRolloutReading Reading, bool Late)> WatchAsync(
        PipelineContext pipeline, IArgoCdClient argo, PocRolloutExpected expected, PocRolloutBudget budget, int pollSeconds,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(argo);
        await RefreshAsync(argo, expected.Application, ct);
        while (true)
        {
            var reading = await ReadAsync(argo, expected, ct);
            await lines.ShowAsync(pipeline, reading.Line, ct);
            if (reading.Kind != PocRolloutReadingKind.Waiting) return (reading, false);
            if (clock.GetUtcNow() >= budget.Until) return (reading, true);
            await Task.Delay(TimeSpan.FromSeconds(pollSeconds), clock, ct);
        }
    }

    private async Task RefreshAsync(IArgoCdClient argo, string application, CancellationToken ct)
    {
        try
        {
            await argo.RefreshAsync(application, ct);
        }
        catch (ArgoCdException ex) when (ex.IsTransient)
        {
            logger.LogInformation(ex, "The refresh of {Application} was not answered; the follow reads on", application);
        }
    }

    private async Task<PocRolloutReading> ReadAsync(IArgoCdClient argo, PocRolloutExpected expected, CancellationToken ct)
    {
        try
        {
            var app = await argo.GetApplicationAsync(expected.Application, ct);
            var live = app is null || expected.Absent ? null
                : await argo.GetLiveDeploymentAsync(expected.Application, expected.Namespace, GitOps.PocManifestNames.App, ct);
            return verdict.Judge(expected, app, live);
        }
        catch (ArgoCdException ex) when (ex.IsTransient)
        {
            logger.LogInformation(ex, "A rollout reading of {Application} was not answered", expected.Application);
            return new PocRolloutReading(PocRolloutReadingKind.Waiting, $"no reading: {ex.Message}");
        }
    }
}
