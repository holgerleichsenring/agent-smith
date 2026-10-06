namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-06-cea8: what a tracker field decides — the <see cref="Models.ConfigStudio.CapabilityField.Group"/>
/// the studio places it by. The wire words are the client contract.
/// </summary>
public static class TrackerFieldGroups
{
    /// <summary>Where the board lives and who signs in.</summary>
    public const string Connection = "connection";

    /// <summary>Which tickets are picked up.</summary>
    public const string Intake = "intake";

    /// <summary>The status a ticket moves to when a run ends one way or another.</summary>
    public const string Outcome = "outcome";

    /// <summary>How the board's own transitions and in-flight states are named.</summary>
    public const string Transition = "transition";

    /// <summary>Which pipeline a claimed ticket runs.</summary>
    public const string Routing = "routing";

    /// <summary>What agent-smith files on the board itself.</summary>
    public const string Filing = "filing";
}
