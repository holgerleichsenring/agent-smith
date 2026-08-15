namespace AgentSmith.Contracts.Commands;

/// <summary>p0408: how a pipeline step uses a model.</summary>
public enum ModelUse
{
    /// <summary>Deterministic machinery — no model runs at all.</summary>
    None,

    /// <summary>One structured call: one prompt, one answer the code parses.</summary>
    Call,

    /// <summary>An agentic loop: the model drives tools until it declares itself done.</summary>
    Loop,
}

/// <summary>
/// p0408: what a step asks a model for. <paramref name="Actor"/> is the master skill or
/// prompt that runs; <paramref name="Answer"/> is the shape the code expects back. An
/// empty Actor on a <see cref="ModelUse.Loop"/> step means the master is resolved per
/// pipeline (<see cref="PipelinePresets.MasterFor"/>).
/// </summary>
public readonly record struct StepModel(ModelUse Use, string Actor = "", string Answer = "");

/// <summary>
/// p0408: the per-command model-use declaration, beside <see cref="CommandBeats"/> and
/// <see cref="CommandStepClasses"/>. It answers the two questions a reader of the control
/// flow asks first — does a model run here, and what is it asked for — for every step of
/// every preset.
/// <para>
/// DECLARED, not inferred: the chat client reaches most handlers through two or three
/// services, so deriving this from constructor dependencies would be a guess presented as
/// evidence. Two tests keep the declaration honest instead —
/// <c>ModelUse_EveryEffectivePresetStep_IsClassified</c> fails when a preset gains a step
/// nobody classified, and <c>ModelUse_HandlerTakingAChatClient_IsDeclaredAsAModelStep</c>
/// fails when a handler starts calling a model without saying so here.
/// </para>
/// </summary>
public static partial class CommandModelUse
{
    /// <summary>The declaration for a command; deterministic for anything unclassified,
    /// so a future command is never DRAWN as running a model it does not run.</summary>
    public static StepModel For(string commandName) =>
        Steps.TryGetValue(commandName, out var step) ? step : new StepModel(ModelUse.None);

    /// <summary>False when a command carries no declaration at all — the condition the
    /// coverage test fails on, distinct from an explicit "no model here".</summary>
    public static bool IsClassified(string commandName) =>
        Steps.ContainsKey(commandName) || Deterministic.Contains(commandName);

    /// <summary>The commands that run a model, for callers that want only those.</summary>
    public static IReadOnlyDictionary<string, StepModel> ModelSteps => Steps;
}
