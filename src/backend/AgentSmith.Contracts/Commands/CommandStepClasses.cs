namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0398: static display class per pipeline command, so the run drawer can show
/// the run's STORY instead of ~27 rows of sub-second mechanics. Three classes:
/// <see cref="Milestone"/> tells the story (fetch, checkout, analyze,
/// derive-spec, master, verify, commit/PR, deliver); <see cref="Gate"/> is
/// visible only when it has a finding (failed, parked, or a summary that is not
/// one of its known no-op sentences); <see cref="Internal"/> is collapsed by
/// default (loaders, splice mechanics, publish/bookkeeping steps).
///
/// The classification lives in the READ path (RunStepsReader classifies by the
/// projected command name), so old run records condense exactly like new ones
/// without rewriting. Unknown or future commands default to
/// <see cref="Milestone"/> — a step is never silently hidden because nobody
/// classified it. CommandStepClassesCoverageTests reflects over every public
/// const string on CommandNames and fails when a constant has no entry here.
/// </summary>
public static partial class CommandStepClasses
{
    public const string Milestone = "milestone";
    public const string Gate = "gate";
    public const string Internal = "internal";

    public static string Get(string? commandName)
    {
        if (string.IsNullOrEmpty(commandName)) return Milestone;
        if (Classes.TryGetValue(commandName, out var cls)) return cls;

        // Parameterised shapes ("SkillRoundCommand:architect:1") classify by
        // their base command, mirroring CommandDisplayNames.Get.
        var baseCommand = commandName.Contains(':')
            ? commandName[..commandName.IndexOf(':')]
            : commandName;

        return Classes.TryGetValue(baseCommand, out var baseCls) ? baseCls : Milestone;
    }

    /// <summary>
    /// A gate has NOTHING to say when its summary is one of the sentences its
    /// handler returns for the "everything ordinary, nothing happened" path.
    /// Matching is ordinal-contains so summaries that embed run-specific ids
    /// around the sentence still classify as silent. Only gates have entries;
    /// for every other class the answer is false.
    /// </summary>
    public static bool IsNoOpSummary(string? commandName, string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary)) return true;
        if (string.IsNullOrEmpty(commandName)) return false;
        return GateNoOpSummaries.TryGetValue(commandName, out var phrases)
            && phrases.Any(p => summary.Contains(p, StringComparison.Ordinal));
    }

    public static IReadOnlyDictionary<string, string> All => Classes;
}
