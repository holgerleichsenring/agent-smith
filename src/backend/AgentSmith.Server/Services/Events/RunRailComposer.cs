using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0405: composes the run's rail as ONE ordered sequence — the steps that ran,
/// followed by the ones the executor announced but has not reached. The client
/// consumes this; it does not derive it. Splitting the derivation across two
/// languages is what would make it unmaintainable, and a dashboard that
/// multiplied a block by a phase count would be guessing at what the server knows.
/// </summary>
public sealed class RunRailComposer
{
    /// <summary>
    /// Appends the announced tail beyond the last executed step. A TERMINAL run
    /// gets none: nothing is still coming for a run that has stopped, whether it
    /// finished its sequence or was cut short.
    /// </summary>
    public IReadOnlyList<RunStepView> Compose(IReadOnlyList<RunStepView> executed, Run? run)
    {
        if (run is null || run.FinishedAt is not null) return executed;
        var planned = RunStoryJson.TryDeserialize<List<PlannedStepView>>(run.PlannedStepsJson);
        if (planned is null || planned.Count == 0) return executed;

        // The last STARTED step is where the run is; anything the announcement
        // places beyond it has not been reached. Announcements carry absolute step
        // indexes, so a resumed run's tail lines up with the rows it continues.
        var reached = executed.Count == 0 ? int.MinValue : executed.Max(s => s.StepIndex);
        return [.. executed, .. planned
            .Where(p => p.StepIndex > reached)
            .OrderBy(p => p.StepIndex)
            .Select(RunStepView.ForPlanned)];
    }
}
