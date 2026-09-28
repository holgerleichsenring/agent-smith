using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.ChatRuns;

/// <summary>
/// Runs the chat-run follower on every replica, from startup on, every few seconds. Nothing is
/// held in memory between passes: the open bindings are re-read each time, so a restart, a new
/// replica or an expired conversation state loses nothing. Which replica posts is decided by
/// the store's conditional writes, not by which one runs this.
/// </summary>
public sealed class ChatRunBindingWatcher(
    ChatRunFollower follower,
    TimeProvider timeProvider,
    ILogger<ChatRunBindingWatcher> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Chat-run binding watcher started (every {Interval})", Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await follower.FollowOnceAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Chat-run binding pass failed"); }

            try { await Task.Delay(Interval, timeProvider, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
