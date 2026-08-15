using AgentSmith.Contracts.Commands;

namespace AgentSmith.Tests.ControlFlow;

/// <summary>One step of a run, as the executor will meet it.</summary>
/// <param name="Command">The typed command name.</param>
/// <param name="Label">The operator-facing label the dashboard shows for it.</param>
/// <param name="Beat">The run-story beat it belongs to.</param>
/// <param name="Class">milestone / gate / internal, from the read-path classification.</param>
/// <param name="Model">Whether a model runs, who answers, and what is expected back.</param>
/// <param name="InPhaseBlock">True for the steps spliced once per derived phase spec.</param>
internal sealed record StepFact(
    string Command, string Label, RunBeat Beat, string Class, StepModel Model, bool InPhaseBlock);

/// <summary>One pipeline preset: its steps in execution order and the master it loads.</summary>
internal sealed record PresetFlow(string Name, string Master, IReadOnlyList<StepFact> Steps)
{
    public int ModelStepCount => Steps.Count(s => s.Model.Use != ModelUse.None);

    /// <summary>The masters this preset actually drives a loop with — resolved per step, so
    /// a preset without an AgenticMaster step is never labelled with the default master it
    /// would have loaded if it had one.</summary>
    public IReadOnlyList<string> LoopActors =>
        [.. Steps.Where(s => s.Model.Use == ModelUse.Loop).Select(ActorOf).Distinct(StringComparer.Ordinal)];

    /// <summary>The master a step runs — the per-pipeline default when the step's own
    /// declaration leaves the actor open, which is exactly the AgenticMaster case.</summary>
    public string ActorOf(StepFact step) =>
        step.Model.Actor.Length > 0 ? step.Model.Actor : Master;
}

/// <summary>
/// p0408: reads the control flow out of the code that defines it —
/// <see cref="PipelinePresets.Effective"/> (the per-phase block already spliced),
/// <see cref="CommandDisplayNames"/>, <see cref="CommandBeats"/>,
/// <see cref="CommandStepClasses"/> and <see cref="CommandModelUse"/>. Nothing here
/// restates the flow; it only assembles what those declarations already say.
/// </summary>
internal static class ControlFlowFacts
{
    /// <summary>The preset the diagram draws in full — the one that ships code.</summary>
    public const string SpineName = PipelinePresets.CodeName;

    public static IReadOnlyList<PresetFlow> All() =>
        [.. Ordered().Select(Of)];

    public static PresetFlow Of(string pipelineName)
    {
        var literal = PipelinePresets.TryResolve(pipelineName) ?? [];
        var spliceAt = literal.ToList().IndexOf(CommandNames.PhaseSequence);
        var blockEnd = spliceAt < 0 ? -1 : spliceAt + PipelinePresets.CodePhaseBlock.Count;
        var steps = PipelinePresets.Effective(pipelineName)
            .Select((c, i) => StepOf(c, i >= spliceAt && i < blockEnd));
        return new PresetFlow(pipelineName, PipelinePresets.MasterFor(pipelineName), [.. steps]);
    }

    /// <summary>Spine first, then the rest alphabetically — a stable order, so the
    /// generated file changes only when the flow changes.</summary>
    private static IEnumerable<string> Ordered() =>
        [SpineName, .. PipelinePresets.Names
            .Where(n => !string.Equals(n, SpineName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.Ordinal)];

    private static StepFact StepOf(string command, bool inPhaseBlock) =>
        new(command,
            CommandDisplayNames.Get(command),
            CommandBeats.TryGet(command, out var beat) ? beat : RunBeat.Building,
            CommandStepClasses.Get(command),
            CommandModelUse.For(command),
            inPhaseBlock);
}
