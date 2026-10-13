namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0203: central operator-facing display labels for every pipeline command.
/// Surfaces in StepStartedEvent.DisplayName so the dashboard's execution
/// tree shows "Setup private-feed credentials" instead of the C# class
/// name "SetupRegistryAuthCommand". Single source of truth for execution-
/// tree labels — distinct from CommandNames.GetLabel which feeds the
/// Slack/Teams/CLI progress reporter with present-continuous phrases
/// ("Setting up credentials"). The display-name form is the operator's
/// noun-phrase reading of what the step is.
///
/// CommandDisplayNamesCoverageTests reflects against every public const
/// string on CommandNames + its nested partial classes (Pipeline / Api /
/// Security) and fails if a constant has no entry here. Adding a new
/// CommandName without a label is therefore caught by the test suite, not
/// by an operator hitting the dashboard.
/// </summary>
public static partial class CommandDisplayNames
{
    public static string Get(string commandName)
    {
        if (Labels.TryGetValue(commandName, out var label))
            return label;

        var baseCommand = commandName.Contains(':')
            ? commandName[..commandName.IndexOf(':')]
            : commandName;

        return Labels.TryGetValue(baseCommand, out var baseLabel)
            ? baseLabel
            : commandName;
    }

    public static IReadOnlyDictionary<string, string> All => Labels;
}
