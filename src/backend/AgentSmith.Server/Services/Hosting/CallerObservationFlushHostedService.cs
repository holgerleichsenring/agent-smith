using AgentSmith.Contracts.Services;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.Hosting;

/// <summary>
/// 2026-08-26-7a51: drains the observation buffer onto the observed-caller table, off the
/// request path.
/// <para>
/// This is the whole reason the buffer exists. A row per validated token would be one
/// upsert per request per caller, on SQLite taking the write lock inside the authorization
/// handler, with the dashboard's polling and the hub handshake multiplying it. Here a
/// window's worth of callers is one batch, and a failed batch goes back into the buffer so
/// a caller suppressed by a flush that never landed is not lost until they stop calling.
/// </para>
/// </summary>
internal sealed class CallerObservationFlushHostedService(
    IObservedCallerStore store,
    CallerObservationBuffer buffer,
    ILogger<CallerObservationFlushHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(FlushInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
            await FlushAsync(stoppingToken);
        }
    }

    internal async Task FlushAsync(CancellationToken cancellationToken)
    {
        var pending = buffer.Drain();
        if (pending.Count == 0) return;
        try
        {
            await store.UpsertAsync(pending, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fail open in both directions: nobody was refused to get here, and nobody is
            // forgotten because of it either.
            logger.LogWarning(ex, "{Count} observed caller(s) could not be written", pending.Count);
            buffer.Reinstate(pending);
        }
    }
}
