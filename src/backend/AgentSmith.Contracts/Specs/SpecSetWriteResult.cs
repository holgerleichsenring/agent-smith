namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: the outcome of committing one revision. A failed write leaves the run
/// working from the in-memory set: the artifacts are how a REVIEWER reads the
/// derivation, and losing the reviewer's view must not lose the run.
/// <para>
/// 2026-10-12-f5f7d: one sha per repository written (<see cref="Shas"/>; <see cref="CommitSha"/> is
/// the carrying repository's), and a REFUSED write — a repository the series names is not checked
/// out — is told apart from one that failed on the way.
/// </para>
/// <para>
/// 2026-10-12-f5f7f: whether each member was pushed in this write (<see cref="Pushed"/>), and for a
/// multi-repository write that failed, the revert commit each pushed member got
/// (<see cref="RevertShas"/>) — such a write is <see cref="IsRolledBack"/> and stops the run.
/// </para>
/// </summary>
public sealed record SpecSetWriteResult(bool Written, string? CommitSha, string? Error)
{
    public IReadOnlyDictionary<string, string> Shas { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, bool> Pushed { get; init; } =
        new Dictionary<string, bool>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> RevertShas { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public bool IsRefused { get; init; }

    public bool IsRolledBack { get; init; }

    public static SpecSetWriteResult Ok(string sha) => new(true, sha, null);

    public static SpecSetWriteResult Ok(IReadOnlyDictionary<string, string> shas, string sha) =>
        new(true, sha, null) { Shas = shas };

    public static SpecSetWriteResult Failed(string error) => new(false, null, error);

    public static SpecSetWriteResult Refused(string error) => new(false, null, error) { IsRefused = true };

    /// <summary>A multi-repository write that failed in one member and was undone in the others.</summary>
    public static SpecSetWriteResult RolledBack(
        string error, IReadOnlyDictionary<string, bool> pushed, IReadOnlyDictionary<string, string> reverts) =>
        new(false, null, error) { Pushed = pushed, RevertShas = reverts, IsRolledBack = true };
}
