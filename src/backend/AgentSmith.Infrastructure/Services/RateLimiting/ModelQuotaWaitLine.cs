using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Turns;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// 2026-11-22-66e4a: the line a run shows while its calls wait for model quota, on the path 24bd's
/// <see cref="ModelRunWaitLine"/> uses — a step line on the run and a waiting step on a design turn. One run shows
/// one line however many of its calls wait (a refusal pauses them all), and a line of its own when the last of them
/// goes on, which is where the Now block ends the wait: the waiting call's LlmCallStarted came before it. A line
/// that cannot be shown never fails the call.
/// </summary>
public sealed class ModelQuotaWaitLine(
    IRunContextAccessor runContext,
    ITurnActivityObserverAccessor turnActivity,
    IEventPublisher events,
    ILogger<ModelQuotaWaitLine> logger)
{
    /// <summary>The step-detail origin of the waiting line.</summary>
    public const string Origin = "model-quota";

    /// <summary>The step-detail origin of the line that ends the wait.</summary>
    public const string OverOrigin = "model-quota-over";

    /// <summary>2026-11-22-ab45: the step-detail origin of the line that says a call cannot fit a minute's quota.</summary>
    public const string CannotFitOrigin = "model-quota-cannot-fit";

    private readonly Dictionary<string, int> _waiting = new(StringComparer.Ordinal);
    private readonly RecentlyToldRuns _told = new();

    public static string Text(string deployment, TimeSpan wait, string reason) =>
        $"Waiting {Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds))} s for model quota on {deployment}: {reason}";

    public static string OverText(string deployment) => $"Model quota free again on {deployment}";

    /// <summary>A call of the ambient run starts waiting about <paramref name="wait"/>.</summary>
    public async Task WaitingAsync(string deployment, TimeSpan wait, string reason, CancellationToken cancellationToken)
    {
        var runId = runContext.CurrentRunId;
        var text = Text(deployment, wait, reason);
        logger.LogInformation("Run {RunId}: {Waiting}", runId, text);
        if (!Joined(runId)) return;
        await ShowAsync(runId, Origin, new TurnActivity(TurnActivityKind.Waiting, null, text), text, cancellationToken);
    }

    /// <summary>A call of the ambient run goes on; the last one ends the line.</summary>
    public async Task OverAsync(string deployment, CancellationToken cancellationToken)
    {
        var runId = runContext.CurrentRunId;
        if (!Left(runId)) return;
        var text = OverText(deployment);
        await ShowAsync(runId, OverOrigin, new TurnActivity(TurnActivityKind.Thinking, deployment), text, cancellationToken);
    }

    /// <summary>2026-11-22-ab45: once per run and deployment, in the log, on the run and on a design turn: a call does not
    /// fit a minute's quota (<paramref name="sentence"/> says how). A call outside a run is told every time; its notice
    /// already says it once per call.</summary>
    public async Task CannotFitAsync(string deployment, string sentence, CancellationToken cancellationToken)
    {
        var runId = runContext.CurrentRunId;
        if (!string.IsNullOrEmpty(runId) && !_told.FirstTime(deployment, runId)) return;
        logger.LogWarning("Run {RunId}: {CannotFit}", runId, sentence);
        await ShowAsync(runId, CannotFitOrigin, new TurnActivity(TurnActivityKind.Waiting, null, sentence), sentence, cancellationToken);
    }

    // True for the first waiting call of a run; a call outside a run always shows.
    private bool Joined(string? runId)
    {
        if (string.IsNullOrEmpty(runId)) return true;
        lock (_waiting)
        {
            _waiting[runId] = _waiting.GetValueOrDefault(runId) + 1;
            return _waiting[runId] == 1;
        }
    }

    // True for the last waiting call of a run.
    private bool Left(string? runId)
    {
        if (string.IsNullOrEmpty(runId)) return true;
        lock (_waiting)
        {
            if (!_waiting.TryGetValue(runId, out var count)) return false;
            if (count > 1) { _waiting[runId] = count - 1; return false; }
            _waiting.Remove(runId);
            return true;
        }
    }

    private async Task ShowAsync(
        string? runId, string origin, TurnActivity? activity, string text, CancellationToken cancellationToken)
    {
        try
        {
            if (activity is not null) await turnActivity.ReportAsync(activity, cancellationToken);
            if (!string.IsNullOrEmpty(runId))
                await events.PublishAsync(
                    new L1StepDetailEvent(runId, runContext.CurrentStepIndex ?? 0, origin, text, DateTimeOffset.UtcNow),
                    cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Run {RunId}: the model quota line could not be shown", runId);
        }
    }
}
