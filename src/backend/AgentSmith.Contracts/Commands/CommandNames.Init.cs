namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0490: command names owned by the init-project pipeline's tail. Init opens one
/// pull request per repo and, when the operator asked for it at launch, finishes them.
/// </summary>
public static partial class CommandNames
{
    /// <summary>p0490: completes the pull requests InitCommit opened, one repo at a
    /// time, when the launch carried the operator's auto-accept. Runs LAST — after
    /// PrCrossLink, whose second pass PATCHes each sibling's body and would either be
    /// lost or fail against a closed pull request. A completion a branch policy refuses
    /// leaves the pull request open with the reason recorded, and does not fail the
    /// run.</summary>
    public const string InitComplete = "InitCompleteCommand";

    /// <summary>2026-09-23-4711: moves aside every context directory the repository carries
    /// that this run's derivation did not produce. Emitted by BootstrapDispatch as the LAST
    /// of its follow-ups, so it runs once every round has written — a round that fails stops
    /// the pipeline before it and retires nothing. Never a finalizer, for the same
    /// reason.</summary>
    public const string BootstrapRetire = "BootstrapRetireCommand";
}
