namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-10-16-86e6a: why a run ended, recorded by the code that ended it — never read from its summary.
/// Null on a run whose end no producer classified.
/// </summary>
public static class RunEndKinds
{
    /// <summary>Preflight refused a context image outside the trusted registries.</summary>
    public const string ImageUntrusted = "image-untrusted";

    /// <summary>Preflight refused another precondition.</summary>
    public const string Preflight = "preflight";

    /// <summary>The bootstrap gate found a repository without its agent-smith bootstrap.</summary>
    public const string BootstrapMissing = "bootstrap-missing";

    /// <summary>2026-11-16-f3fba: the host could not say whether a repository carries its bootstrap — an
    /// unreachable host or an unreadable branch, not a verdict on the repository.</summary>
    public const string ReadinessUnknown = "readiness-unknown";

    /// <summary>The acceptance gate left a criterion of the ratified spec outstanding.</summary>
    public const string CriteriaUnmet = "criteria-unmet";

    /// <summary>A parked attempt closed because a newer attempt of the ticket started past it.</summary>
    public const string Superseded = "superseded";
}
