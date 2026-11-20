using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Turns;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// 2026-11-20-24bd: the one line a run waiting for its model place shows — a step line on the run (the Now block
/// shows it until the run's next model call starts) and a waiting step on a design turn. It names the model, the
/// limit and the config key; a line that cannot be shown never fails the call.
/// </summary>
public sealed class ModelRunWaitLine(
    IRunContextAccessor runContext,
    ITurnActivityObserverAccessor turnActivity,
    IEventPublisher events,
    ILogger<ModelRunWaitLine> logger)
{
    /// <summary>The step-detail origin of the waiting line.</summary>
    public const string Origin = "model-slot";

    /// <summary>The waiting line for <paramref name="agent"/> at <paramref name="limit"/>.</summary>
    public static string Text(AgentConfig agent, int limit) =>
        $"Waiting for the model: {ModelRunSlotKey.Model(agent)} runs {limit} at a time ({AgentConfig.MaxConcurrentRunsKey})";

    /// <summary>Shows that <paramref name="runId"/> waits for a place on <paramref name="agent"/>'s deployment.</summary>
    public async Task ShowAsync(AgentConfig agent, int limit, string runId, CancellationToken cancellationToken)
    {
        var text = Text(agent, limit);
        logger.LogInformation("Run {RunId}: {Waiting}", runId, text);
        try
        {
            await turnActivity.ReportAsync(new TurnActivity(TurnActivityKind.Waiting, null, text), cancellationToken);
            await events.PublishAsync(
                new L1StepDetailEvent(runId, runContext.CurrentStepIndex ?? 0, Origin, text, DateTimeOffset.UtcNow),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Run {RunId}: the waiting line could not be shown", runId);
        }
    }
}
