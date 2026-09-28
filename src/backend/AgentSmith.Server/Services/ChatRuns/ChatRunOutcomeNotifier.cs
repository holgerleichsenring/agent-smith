using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.ChatRuns;

/// <summary>
/// Posts a run's outcome to the thread that started it and frees the thread. The binding is
/// closed FIRST and only the caller that closed it posts, so a run followed by every replica is
/// reported exactly once.
/// </summary>
public sealed class ChatRunOutcomeNotifier(
    IChatRunBindingStore bindings,
    ChatThreadAdapters adapters,
    ChatRunOutcomeText text,
    ILogger<ChatRunOutcomeNotifier> logger)
{
    public async Task<bool> NotifyAsync(ChatRunBindingFact binding, RunOutcome? outcome, CancellationToken ct)
    {
        if (!await bindings.TryCloseAsync(binding.RunId, ct)) return false;
        await adapters.PostAsync(ChatThread.Of(binding), text.Compose(binding.RunId, outcome), ct);
        logger.LogInformation(
            "Run {RunId} reported {Status} to its chat thread on {Platform} {Channel}",
            binding.RunId, outcome?.Status ?? "gone", binding.Platform, binding.ChannelId);
        return true;
    }
}
