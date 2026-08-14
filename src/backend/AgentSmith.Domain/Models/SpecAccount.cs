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
    /// <summary>Nothing outstanding — and an account that could not be taken is not a pass.</summary>
    public bool Delivered => Problem is null && Criteria.Count > 0 && Criteria.All(c => c.Satisfied);

    public IReadOnlyList<CriterionAccount> Outstanding => [.. Criteria.Where(c => !c.Satisfied)];
}
