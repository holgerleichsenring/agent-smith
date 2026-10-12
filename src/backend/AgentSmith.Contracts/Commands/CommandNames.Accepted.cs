namespace AgentSmith.Contracts.Commands;

/// <summary>
/// 2026-10-12-967aa / 967ab: the command names the spec block was written under before the
/// rename, read as the names this release declares. A renamed command is NOT retired —
/// <see cref="RetiredCommands"/> throws — so a stored step row, a planned step, a checkpoint or an
/// operator's preset carrying the old name resolves to the same command, while every name this
/// release writes is the declared one. 967ah removes the table once every store is rewritten.
/// </summary>
public static partial class CommandNames
{
    private static readonly IReadOnlyDictionary<string, string> AcceptedCommandNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["WritePhaseRecordCommand"] = WriteSpecRecord,
            ["VerifyPhaseCommand"] = VerifySpec,
            ["CommitPhaseWorkCommand"] = CommitSpecWork,
            ["CheckPhasePremisesCommand"] = CheckSpecPremises,
            ["ReviewPhaseDiffCommand"] = ReviewSpecDiff,
            ["PhaseSequenceCommand"] = SpecSequence,
            ["SelectPhaseCommand"] = SelectSpec,
            ["PhaseSpecGateCommand"] = SpecGate,
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
