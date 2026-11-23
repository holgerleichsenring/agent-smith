namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-09-22-b6ad: one repo-relative file a checkout-free write puts on a branch. Text only —
/// what this write carries is a specification set, and a binary would need a second encoding
/// on all four providers for a case nothing has.
/// <para>
/// 2026-11-23-c81ea: or one it REMOVES — <see cref="Deletion"/>, with <see cref="Delete"/> true and no
/// content. A deletion of a path the branch does not hold is dropped and said in the result's notes.
/// </para>
/// </summary>
/// <param name="Path">Repo-relative, forward slashes, no leading slash.</param>
/// <param name="Content">The file's whole content; the write REPLACES what is there. Empty for a deletion.</param>
public sealed record RepoFile(string Path, string Content)
{
    /// <summary>True when the write removes <see cref="Path"/> instead of writing it.</summary>
    public bool Delete { get; init; }

    /// <summary>A removal of <paramref name="path"/>.</summary>
    public static RepoFile Deletion(string path) => new(path, string.Empty) { Delete = true };
}

/// <summary>
/// 2026-09-22-b6ad: what a checkout-free branch write produced — the commit it made, or the
/// reason it made none.
/// <para>
/// A write that cannot answer a sha is the contract's FAILURE case, never a quiet success: the
/// caller records that sha as the pointer at the spec path, and an absent pointer is read as
/// somebody else's edit — exactly what recording a pointer exists to prevent. A provider that
/// wrote files but has no commit to name must therefore say <see cref="Failed"/>.
/// </para>
/// <para>
/// 2026-11-23-c81ea: two refusals carry a meaning of their own, and <see cref="Written"/> stays false for
/// both. <see cref="Conflict"/>: the branch head moved between the read and the ref update, so a caller may
/// re-plan from the new head and write again. <see cref="AlreadyHeld"/>: every entry was a deletion of a
/// path the branch does not hold, so there was nothing to commit.
/// </para>
/// </summary>
/// <param name="Notes">Sentences about files the write left out without failing; null when none.</param>
public sealed record BranchWriteResult(string? CommitSha, string? Error, IReadOnlyList<string>? Notes = null)
{
    /// <summary>The answer a write whose every deletion was already absent carries as its error.</summary>
    public const string AllDeletionsAbsent = "every deletion was already absent";

    public bool Written => Error is null && !string.IsNullOrWhiteSpace(CommitSha);

    /// <summary>True only when the host refused the ref update because the branch head moved.</summary>
    public bool Conflict { get; init; }

    /// <summary>True only when every entry was a deletion of a path the branch does not hold.</summary>
    public bool AlreadyHeld { get; init; }

    public static BranchWriteResult Ok(string sha) => new(sha, null);

    public static BranchWriteResult Failed(string error) => new(null, error);

    /// <summary>The branch head moved under the write; a caller may re-plan on it.</summary>
    public static BranchWriteResult Conflicted(string reason) => new(null, reason) { Conflict = true };

    /// <summary>Nothing to commit: every deletion named a path already absent, each said in <paramref name="notes"/>.</summary>
    public static BranchWriteResult HeldAlready(IReadOnlyList<string> notes) =>
        new(null, AllDeletionsAbsent, notes) { AlreadyHeld = true };
}
