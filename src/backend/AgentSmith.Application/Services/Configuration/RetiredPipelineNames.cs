using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// The one list of pipeline names this product no longer runs, and why: the coding preset names
/// collapsed into <c>code</c> (what each one became), and the presets removed outright (the
/// reason, and what bringing one back would take).
/// <para>
/// This is NOT a resolver and must never become one. Nothing routes, classifies, sizes or parks a
/// run through it: <c>PipelinePresets.TryResolve</c> and <c>IsAcceptedName</c> do not consult it,
/// which is the whole point — a name in here does not run. Every reader is addressed to a PERSON:
/// the one-shot migration that rewrites a stored configuration off the renamed names, the startup
/// findings, and every refusal of a pipeline name, which say what the name became or why it went.
/// </para>
/// </summary>
public static class RetiredPipelineNames
{
    /// <summary>Renamed name -> the preset that replaced it.</summary>
    public static readonly IReadOnlyDictionary<string, string> Replacements =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fix-bug"] = PipelinePresets.CodeName,
            ["fix-no-test"] = PipelinePresets.CodeName,
            ["add-feature"] = PipelinePresets.CodeName,
            ["phase-execution"] = PipelinePresets.CodeName,
        };

    /// <summary>Removed name -> why it went, with nothing that replaces it.</summary>
    public static readonly IReadOnlyDictionary<string, string> Removed =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["skill-manager"] =
                "skill-manager was removed together with the Triage/SkillRound machinery it was "
                + "the last consumer of. Bringing it back means authoring a skill-manager master "
                + "and declaring an AgenticMaster-shaped preset.",
            ["autonomous"] =
                "autonomous was removed together with the Triage/SkillRound machinery it was "
                + "the last consumer of. Bringing it back means authoring an autonomous master "
                + "and declaring an AgenticMaster-shaped preset.",
        };

    /// <summary>The preset that replaced <paramref name="pipelineName"/>, or null when the name
    /// was never one of the renamed four.</summary>
    public static string? ReplacementFor(string pipelineName) =>
        Replacements.GetValueOrDefault(pipelineName);

    /// <summary>
    /// What a person holding <paramref name="pipelineName"/> needs to hear: the name that replaced
    /// it, or why it was removed. Null when the name was never retired — a typo, not a history.
    /// </summary>
    public static string? Explain(string pipelineName) =>
        ReplacementFor(pipelineName) is { } target
            ? $"'{pipelineName}' was retired into '{target}'. Write '{target}' instead."
            : Removed.GetValueOrDefault(pipelineName);

    /// <summary>The refusal for a pipeline name that does not run: the explanation when the name
    /// was retired, otherwise the names that do run.</summary>
    public static string Refusal(string pipelineName) =>
        $"Pipeline '{pipelineName}' is not a pipeline this product runs. "
        + (Explain(pipelineName) ?? $"Offered: {string.Join(", ", PipelinePresets.Names)}.");
}
