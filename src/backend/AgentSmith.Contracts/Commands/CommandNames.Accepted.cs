namespace AgentSmith.Contracts.Commands;

/// <summary>
/// 2026-10-12-967aa: the command names a later release writes for the spec block, read as the
/// names this release declares. A renamed command is NOT retired — <see cref="RetiredCommands"/>
/// throws — so a stored step row, a planned step or a checkpoint carrying the new name resolves
/// to the same command here, while every name this release writes stays the declared one.
/// </summary>
public static partial class CommandNames
{
    private static readonly IReadOnlyDictionary<string, string> AcceptedCommandNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["WriteSpecRecordCommand"] = WriteSpecRecord,
            ["VerifySpecCommand"] = VerifySpec,
            ["CommitSpecWorkCommand"] = CommitSpecWork,
            ["CheckSpecPremisesCommand"] = CheckSpecPremises,
            ["ReviewSpecDiffCommand"] = ReviewSpecDiff,
            ["SpecSequenceCommand"] = SpecSequence,
            ["SelectSpecCommand"] = SelectSpec,
            ["SpecGateCommand"] = SpecGate,
        };

    /// <summary>
    /// The declared name for <paramref name="commandName"/>: an accepted name maps to the command
    /// it names, a ":param" suffix is kept, and anything else is returned unchanged.
    /// </summary>
    public static string Declared(string commandName)
    {
        ArgumentNullException.ThrowIfNull(commandName);
        var colon = commandName.IndexOf(':');
        var baseName = colon < 0 ? commandName : commandName[..colon];
        return AcceptedCommandNames.TryGetValue(baseName, out var declared)
            ? declared + (colon < 0 ? string.Empty : commandName[colon..])
            : commandName;
    }
}
