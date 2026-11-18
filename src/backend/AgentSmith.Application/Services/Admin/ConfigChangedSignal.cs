using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Admin;

/// <summary>
/// 2026-11-17-f3a8c: tells every replica the configuration changed — the reload epoch bumped and ConfigChanged
/// published — after a write committed. Best effort: the write stands whether the signal reaches anyone or not.
/// </summary>
public sealed class ConfigChangedSignal(IConfigReloadSignal reload, ISystemEventPublisher events, ILogger<ConfigChangedSignal> logger)
{
    public async Task SignalAsync(string source, string actor)
    {
        try
        {
            var epoch = await reload.BumpAsync(CancellationToken.None);
            await events.PublishAsync(new ConfigChangedEvent(source, epoch, actor, DateTimeOffset.UtcNow), CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The configuration change by {Actor} could not be signalled; replicas reload on their next poll", actor);
        }
    }
}
