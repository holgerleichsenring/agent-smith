using System.Diagnostics;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Preflight;

/// <summary>
/// p0324: runs every registered check in registration order and aggregates the
/// outcomes. Never throws: a check that escapes with an exception is reported as a
/// failed outcome (that is a bug in the check — checks classify their own failures),
/// and each check is bounded by a wall-clock timeout so one hung probe cannot stall
/// the whole preflight.
/// </summary>
public sealed class PreflightRunner(
    IEnumerable<IPreflightCheck> checks,
    ILogger<PreflightRunner> logger) : IPreflightRunner
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(60);

    public async Task<PreflightReport> RunAsync(CancellationToken cancellationToken)
    {
        var outcomes = new List<PreflightCheckOutcome>();
        foreach (var check in checks)
            outcomes.Add(await RunOneAsync(check, cancellationToken));
        return new PreflightReport(outcomes);
    }

    private async Task<PreflightCheckOutcome> RunOneAsync(
        IPreflightCheck check, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await ExecuteGuardedAsync(check, cancellationToken);
        return new PreflightCheckOutcome(check.Name, check.Category, result, stopwatch.ElapsedMilliseconds);
    }

    private async Task<PreflightCheckResult> ExecuteGuardedAsync(
        IPreflightCheck check, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(CheckTimeout);
        try
        {
            return await check.RunAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // the caller cancelled the whole preflight — propagate, don't misreport.
        }
        catch (OperationCanceledException ex)
        {
            logger.LogDebug(ex, "Preflight check {Check} timed out", check.Name);
            return PreflightCheckResult.Fail(
                $"timed out after {CheckTimeout.TotalSeconds:0}s",
                "The probed dependency accepted the connection but never answered — check network "
                + "path, proxy, and endpoint host; a hang here surfaces mid-run as a stalled step.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Preflight check {Check} crashed", check.Name);
            return PreflightCheckResult.Fail(
                $"check crashed: {ex.Message}",
                "A crash (instead of a classified failure) is a bug in the check itself — "
                + "re-run with --verbose for the stack trace and report it.");
        }
    }
}
