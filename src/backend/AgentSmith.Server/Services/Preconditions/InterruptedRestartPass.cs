using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Preconditions;

/// <summary>
/// 2026-11-22-6d85c: the automatic restart of attempts a lost process ended — the server stopped under them, or the
/// liveness reaper found them without a driver. Each is retried unchanged ONCE through the Retry a person's button takes
/// (<see cref="IRunRetry"/>, requester <see cref="AttemptChannels.RestartActor"/>): an attempt that restart caused is not
/// restarted again, and Retry unchanged stays a person's act after that. Run beside the precondition pass on its leader
/// lease and interval, over the interruptions <see cref="InterruptedRestartWindow"/> covers.
/// </summary>
public sealed class InterruptedRestartPass(
    IInterruptedAttemptReader interrupted, IServiceScopeFactory scopes, InterruptedRestartWindow window,
    ILogger<InterruptedRestartPass> logger)
{
    /// <summary>Answers how many retries the pass asked for.</summary>
    public async Task<int> RunAsync(AgentSmithConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);
        var asked = 0;
        foreach (var attempt in await interrupted.ListAsync(window.Since, ct))
            if (config.Projects.ContainsKey(attempt.Project) && await RetryAsync(attempt, ct)) asked++;
        if (asked > 0) logger.LogInformation("{Count} attempt(s) a lost process ended were retried unchanged", asked);
        return asked;
    }

    private async Task<bool> RetryAsync(InterruptedAttempt attempt, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRunRetry>()
            .RetryAsync(attempt.RunId, AttemptChannels.RestartActor, RetryOrigin.Restart, ct);
        if (result.Kind == RunRetryResultKind.Requested) return true;
        logger.LogInformation("Attempt {RunId} of {Project}/{Ticket} was interrupted and not restarted: {Why}",
            attempt.RunId, attempt.Project, attempt.TicketId, result.Text ?? result.Kind.ToString());
        return false;
    }
}
