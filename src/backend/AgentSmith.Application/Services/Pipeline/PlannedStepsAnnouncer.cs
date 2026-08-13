using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Pipeline;

/// <summary>
/// p0405: announces the steps the run is going to execute, from a given step
/// index onwards. The executor holds the live command list — so the sequence is
/// REPORTED by the one component that knows it, instead of being re-derived by a
/// reader from a preset name and a step count.
/// <para>
/// Announced when the list is established and again whenever a handler splices
/// into it, which is the only way it changes. Silent when nothing changed, so a
/// 45-step run pays for two announcements rather than forty-five.
/// </para>
/// </summary>
public sealed class PlannedStepsAnnouncer(IEventPublisher eventPublisher)
{
    /// <summary>
    /// Publishes the sequence when it differs from <paramref name="lastAnnounced"/>
    /// and returns the announcement to compare the next one against.
    /// </summary>
    public async Task<string?> AnnounceChangedAsync(
        PipelineContext context, int firstStepIndex, IEnumerable<PipelineCommand> commands,
        string? lastAnnounced, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var steps = Compose(firstStepIndex, commands);
        var json = RunStoryJson.Serialize(steps);
        if (json == lastAnnounced) return lastAnnounced;
        if (!context.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId))
            return json;
        await eventPublisher.PublishAsync(
            new PipelineStepsPlannedEvent(runId!, firstStepIndex, json, DateTimeOffset.UtcNow), ct);
        return json;
    }

    // Step indexes are the executionCount the runner stamps on StepStarted, so a
    // planned entry and the executed row it becomes carry the same index.
    private static List<PlannedStepView> Compose(
        int firstStepIndex, IEnumerable<PipelineCommand> commands) =>
        [.. commands.Select((cmd, offset) => new PlannedStepView(
            firstStepIndex + offset,
            cmd.Name,
            StepLabelComposer.PlainDisplayName(cmd),
            string.IsNullOrEmpty(cmd.PhaseId) ? null : cmd.PhaseId))];
}
