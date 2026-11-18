namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-08-25-7f5a: who decided the dispositions in an <see cref="AcceptanceView"/>.
/// <para>
/// An independent account of the branch and the agent's own report of its work are not the
/// same evidence, and a page that shows them identically invites the second to be read as the
/// first.
/// </para>
/// </summary>
public static class AcceptanceSources
{
    /// <summary>The p0420 delivery account: a fresh instance reading the branch. 2026-11-18-c8e2c: today only a scan's
    /// coverage account; stored spec runs before then carry it too.</summary>
    public const string DeliveryAccount = "delivery_account";

    /// <summary>The p0340 master verification: the master's report — 2026-11-18-c8e2c: the spec's account, written by
    /// the master that led the work and checked it.</summary>
    public const string MasterVerification = "master_verification";
}

/// <summary>
/// Criterion-status vocabulary of <see cref="AcceptanceCriterionView"/>. "unproven" is not a
/// softer "unmet": it says nobody measured, which a red build produces and a failed criterion
/// does not.
/// </summary>
public static class AcceptanceCriterionStatuses
{
    public const string Met = "met";
    public const string Unmet = "unmet";
    public const string NotApplicable = "not_applicable";
    public const string Unproven = "unproven";
}
