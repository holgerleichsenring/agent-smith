namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-17-f3a8a: the steps of the runs on the installation itself.</summary>
public static partial class CommandNames
{
    /// <summary>One turn of the admin assistant: its master's loop over the installation's own records and settings.</summary>
    public const string AdminAssistantTurn = "AdminAssistantTurnCommand";

    /// <summary>2026-11-17-f3a8c: applies an approved set of configuration changes through the Config studio's store.</summary>
    public const string ApplyConfigChanges = "ApplyConfigChangesCommand";
}
