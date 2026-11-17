namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0428: the per-run precondition gate's command names. One handler serves both; the name
/// decides which half of the checks runs (2026-11-16-f3fba).
/// </summary>
public static partial class CommandNames
{
    /// <summary>
    /// 2026-11-16-f3fba: the preconditions that need no sandbox — the agent configuration, the
    /// declared secrets, the context images' trust, the toolchain fallback and the repositories'
    /// bootstrap as the host serves it. Runs before CheckoutSource, so a refusal costs no sandbox.
    /// </summary>
    public const string RunConfigPreflight = "RunConfigPreflightCommand";

    /// <summary>
    /// p0428: the preconditions that inspect the sandboxes and the branch: the canonical home
    /// accepts a write, the sandbox user, the injected secrets and what the branch already
    /// carries. Runs after CheckoutSource and before SetupRegistryAuth, the first step a
    /// read-only sandbox home kills. What the branch carries is REPORTED, never refused.
    /// </summary>
    public const string RunPreflight = "RunPreflightCommand";
}
