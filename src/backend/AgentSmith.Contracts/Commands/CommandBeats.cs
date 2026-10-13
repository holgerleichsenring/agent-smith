namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0344b: deterministic command→beat mapping — the single source of truth the
/// server-side beat derivation reads. Keyed by the TYPED command name
/// (<see cref="CommandNames"/> constants), NEVER by display labels; parameterised
/// forms ("SkillRoundCommand:architect:1") resolve via their base command like
/// <see cref="CommandDisplayNames.Get"/>. CommandBeatsCoverageTests reflects over
/// every public const string on CommandNames and fails when a command has no
/// beat, so a new command cannot silently fall out of the storybar.
/// </summary>
public static partial class CommandBeats
{
    public static bool TryGet(string commandName, out RunBeat beat)
    {
        if (Beats.TryGetValue(commandName, out beat)) return true;

        var baseCommand = commandName.Contains(':')
            ? commandName[..commandName.IndexOf(':')]
            : commandName;
        return Beats.TryGetValue(baseCommand, out beat);
    }

    public static IReadOnlyDictionary<string, RunBeat> All => Beats;
}
