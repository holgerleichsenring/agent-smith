namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-09-22-b6ad: one repo-relative file a checkout-free write puts on a branch. Text only —
/// what this write carries is a specification set, and a binary would need a second encoding
/// on all four providers for a case nothing has.
/// </summary>
/// <param name="Path">Repo-relative, forward slashes, no leading slash.</param>
/// <param name="Content">The file's whole content; the write REPLACES what is there.</param>
public sealed record RepoFile(string Path, string Content);

/// <summary>
/// 2026-09-22-b6ad: what a checkout-free branch write produced — the commit it made, or the
/// reason it made none.
/// <para>
/// A write that cannot answer a sha is the contract's FAILURE case, never a quiet success: the
/// caller records that sha as the pointer at the spec path, and an absent pointer is read as
/// somebody else's edit — exactly what recording a pointer exists to prevent. A provider that
/// wrote files but has no commit to name must therefore say <see cref="Failed"/>.
/// </para>
/// </summary>
public sealed record BranchWriteResult(string? CommitSha, string? Error)
{
    public bool Written => Error is null && !string.IsNullOrWhiteSpace(CommitSha);

    public static BranchWriteResult Ok(string sha) => new(sha, null);

    public static BranchWriteResult Failed(string error) => new(null, error);
}
