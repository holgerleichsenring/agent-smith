namespace AgentSmith.Domain.Models;

/// <summary>
/// p0420: the account a phase gives of itself — one row per ratified criterion, read off
/// the branch rather than off the run.
/// <para>
/// It replaces the question "did THIS run commit code", which judged a resumed branch
/// with a complete delivery as FAILED: nine updated project files, an adapted call site,
/// build and test green, and a verdict of failure. The subject is the delivery, so a
/// phase that finds its work already done passes and says so.
/// </para>
/// </summary>
public sealed record SpecAccount(
    string RepoKey,
    IReadOnlyList<CriterionAccount> Criteria,
    string? Problem = null)
{
    /// <summary>
    /// Nothing outstanding — and an account that could not be taken is not a pass.
    /// <para>
    /// 2026-08-25-9749: nor is an account that satisfied NOTHING, whatever it calls not
    /// applicable. Without that clause the third disposition becomes a way to pass a phase
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
