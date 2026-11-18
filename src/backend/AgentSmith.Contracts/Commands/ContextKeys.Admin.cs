namespace AgentSmith.Contracts.Commands;

/// <summary>2026-11-17-f3a8c: the keys of the runs on the installation.</summary>
public static partial class ContextKeys
{
    /// <summary>The configuration changes an admin turn drafted, continued from the proposal awaiting a decision.</summary>
    public const string AdminConfigDraft = "AdminConfigDraft";

    /// <summary>A config run's request: the changes and the person who applied them.</summary>
    public const string ConfigChangeRequest = "ConfigChangeRequest";

    /// <summary>A config run's report slot, read back by the server after the run.</summary>
    public const string ConfigApplyReport = "ConfigApplyReport";

    /// <summary>2026-11-17-f3a8d: a secret run's request — never serialized into a checkpoint, removed once sealed.</summary>
    public const string SecretWriteRequest = "SecretWriteRequest";

    /// <summary>2026-11-17-f3a8d: a secret run's report slot.</summary>
    public const string SecretWriteReport = "SecretWriteReport";
}
