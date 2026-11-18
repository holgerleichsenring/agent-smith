namespace AgentSmith.Domain.Models;

/// <summary>
/// p0420: the account a spec gives of itself — one row per ratified criterion, read off
/// the branch rather than off the run.
/// <para>
/// It replaces the question "did THIS run commit code", which judged a resumed branch
/// with a complete delivery as FAILED: nine updated project files, an adapted call site,
/// build and test green, and a verdict of failure. The subject is the delivery, so a
/// spec that finds its work already done passes and says so.
/// </para>
/// </summary>
public sealed record SpecAccount(
    string RepoKey,
    IReadOnlyList<CriterionAccount> Criteria,
    string? Problem = null,
    // 2026-09-01-3653: what the run's master pass was given and how far it got. Trailing and
    // optional, so every existing construction site keeps compiling; null on a spec account
    // and on a scan whose master never ran, which OMITS the section rather than zeroing it.
    ScanPassMeasures? Measures = null,
    // 2026-11-18-c8e2c: the rest of the master's report the rows come from — what it fixed beyond the spec's lines and
    // the follow-up specs it proposes. Null on a scan's account and on accounts stored before the report.
    IReadOnlyList<string>? FixedInPassing = null,
    IReadOnlyList<MasterFollowUp>? FollowUps = null,
    // 2026-11-18-98fa: the master's report — its markdown for the person, shown as written. Accounts written since carry
    // no rows and no FixedInPassing; those stay for the accounts stored before.
    string? Report = null)
{
    /// <summary>
    /// Nothing outstanding — and an account that could not be taken is not a pass.
    /// <para>
    /// 2026-08-25-9749: nor is an account that satisfied NOTHING, whatever it calls not
    /// applicable. Without that clause the third disposition becomes a way to pass a spec
    /// having proven nothing at all.
    /// </para>
    /// </summary>
    public bool Delivered =>
        Problem is null && Criteria.Count > 0 && Outstanding.Count == 0 && ProvesSomething;

    /// <summary>The criteria that are a shortfall: not satisfied, and not declined.</summary>
    public IReadOnlyList<CriterionAccount> Outstanding => [.. Criteria.Where(c => c.IsOutstanding)];

    /// <summary>2026-08-25-9749: the criteria the account declined to judge. A refusal has to
    /// be able to name them, or it is a failure with no criterion in it and no repair
    /// possible — the shape the account was built to end.</summary>
    public IReadOnlyList<CriterionAccount> Declined => [.. Criteria.Where(c => c.IsNotApplicable)];

    /// <summary>At least one criterion is positively satisfied by the branch.</summary>
    public bool ProvesSomething => Criteria.Any(c => c.IsSatisfied);
}
