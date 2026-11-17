using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Verify;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-d6fbc: publishes what one verify pass ran as a <see cref="VerifyPassRecordedEvent"/> — per repository its
/// stages in order and whether they came out green, plus the stale derivations — so the dashboard draws a line per
/// repository instead of reading the verdict's prose. The prose stays the step's result for every other reader.
/// </summary>
public sealed class VerifyPassReporter(IEventPublisher events, TimeProvider clock, ILogger<VerifyPassReporter> logger)
{
    public async Task ReportAsync(
        PipelineContext pipeline, IReadOnlyList<VerifyOutcome> outcomes, VerifyResolutionNotes notes, bool passed,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)) return;
        try
        {
            await events.PublishAsync(Build(runId!, outcomes, notes, passed, clock.GetUtcNow()), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The verify pass of run {RunId} was not published as data; its result message stands", runId);
        }
    }

    public VerifyPassRecordedEvent Build(
        string runId, IReadOnlyList<VerifyOutcome> outcomes, VerifyResolutionNotes notes, bool passed, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        ArgumentNullException.ThrowIfNull(notes);
        var repositories = outcomes
            .GroupBy(o => VerifyResolutionNotes.Named(o.Key))
            .Select(g => new VerifyRepositoryPass(
                g.Key,
                g.Any(o => !o.Skipped && o.ExitCode != 0) ? VerifyPassResults.Failed : VerifyPassResults.Passed,
                [.. g.Select(o => new VerifyStageRun(o.Stage, o.Command, o.ExitCode, o.Skipped))]))
            .ToList();
        return new VerifyPassRecordedEvent(
            runId, at, passed ? VerifyPassResults.Passed : VerifyPassResults.Failed, repositories, [.. notes.StaleDerivations]);
    }
}
