namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0428: the per-run precondition gate's command name.
/// </summary>
public static partial class CommandNames
{
    /// <summary>
    /// p0428: proves the run's preconditions in seconds, before the expensive work
    /// starts. Runs after CheckoutSource — the sandboxes and the branch have to exist
    /// to be inspected — and before SetupRegistryAuth, which is the first step a
    /// read-only sandbox home kills. Every check is deterministic and offline: the
    /// configuration is real rather than the empty placeholder, the agent the pipeline
    /// resolves to is configured, configured registry credentials actually carry a
    /// secret, and the canonical home accepts a write. What the branch already carries
    /// is REPORTED, never refused.
    /// </summary>
    public const string RunPreflight = "RunPreflightCommand";
}
