using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.ChatLaunch;

/// <summary>
/// The one way a chat thread starts a run: a thread whose earlier run is still open is refused,
/// otherwise the run is launched, the thread is bound to the run id the launch reported — so
/// the run's questions and its outcome find their way back — and the thread is told.
/// </summary>
public sealed class ChatRunStart(
    IChatRunBindingStore bindings,
    ChatThreadAdapters adapters,
    ChatLaunchAnnouncer announcer,
    TimeProvider timeProvider,
    ILogger<ChatRunStart> logger)
{
    public async Task StartAsync(
        ChatThread thread, string subject,
        Func<CancellationToken, Task<ChatLaunchResult>> launch, CancellationToken ct)
    {
        var result = await BusyAsync(thread, ct) ?? await launch(ct);
        if (result.RunId is { } runId) await BindAsync(thread, runId, ct);
        await announcer.AnnounceAsync(thread, subject, result, ct);
    }

    private async Task<ChatLaunchResult?> BusyAsync(ChatThread thread, CancellationToken ct)
    {
        var open = await bindings.FindOpenInThreadAsync(thread.Platform, thread.ChannelId, thread.ThreadId, ct);
        return open is null
            ? null
            : ChatLaunchResult.Refused(
                $"run `{open.RunId}` started here has not reported back yet. "
                + "Start a new thread for another run, or wait for its outcome.");
    }

    // A binding that cannot be written leaves the run running, just unfollowed from here — the
    // launch already happened, so the thread is still told the run id.
    private async Task BindAsync(ChatThread thread, string runId, CancellationToken ct)
    {
        try
        {
            await bindings.BindAsync(new ChatRunBindingFact(
                runId, thread.Platform, thread.ChannelId, thread.ThreadId, thread.RequestedBy,
                adapters.ReplyEndpointFor(thread.Platform, thread.ChannelId), timeProvider.GetUtcNow()), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Run {RunId} could not be bound to its chat thread {Channel}", runId, thread.ChannelId);
        }
    }
}
