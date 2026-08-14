namespace AgentSmith.Domain.Models;

/// <summary>
/// p0420: what one criterion of the ratified spec is accounted for by.
/// <para>
/// <see cref="Citation"/> is a path the diff is claimed to satisfy it at. It is checked
/// against the diff's own file list, so a criterion cannot be satisfied by a file the
/// phase never touched. That check is about INVENTION, not semantics: a real path may
/// still fail to mean what the account claims, which is the reviewer's twenty seconds.
/// </para>
/// </summary>
/// <param name="Mechanical">
/// True when the answer came from a command, not from a model — a green build is
/// evidence of a different kind and says so in the account.
/// </param>
public sealed record CriterionAccount(
    string Criterion,
    bool Satisfied,
    string? Citation = null,
    string? Note = null,
    bool Mechanical = false);

/// <summary>
/// p0420: the account a phase gives of itself — one row per ratified criterion, read off
/// the branch rather than off the run.
/// <para>
/// It replaces the question "did THIS run commit code", which judged a resumed branch
/// with a complete delivery as FAILED (run c96d, PR !8943: nine updated project files,
/// an adapted call site, build and test green). The subject is the delivery, so a phase
/// that finds its work already done passes and says so.
/// </para>
/// </summary>
public sealed record SpecAccount(
    string RepoKey,
    IReadOnlyList<CriterionAccount> Criteria,
    string? Problem = null)
{
    /// <summary>Nothing outstanding — and an account that could not be taken is not a pass.</summary>
    public bool Delivered => Problem is null && Criteria.Count > 0 && Criteria.All(c => c.Satisfied);

    public IReadOnlyList<CriterionAccount> Outstanding =>
        [.. Criteria.Where(c => !c.Satisfied)];
}
