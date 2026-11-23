using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-10-12-6bfaa: reading a NAMED branch without a checkout. The default-branch reads of the
/// main contract answer for the repository as merged; a run works on its own branch, and what
/// that branch declares — its contexts, and with them its toolchain image — is not on the
/// default branch until somebody merges it.
/// <para>
/// A PARTIAL of its own file for the reason the targeting and branch-write members are one.
/// </para>
/// </summary>
public partial interface ISourceProvider
{
    /// <summary>
    /// Whether <paramref name="branch"/> exists on the remote. False only when the host says the
    /// branch is not there; auth and transport errors propagate, because "absent" and "could not
    /// be asked" lead the caller to different answers.
    /// </summary>
    Task<bool> BranchExistsAsync(BranchName branch, CancellationToken cancellationToken);

    /// <summary>
    /// <see cref="TryReadFileAsync(string, CancellationToken)"/> on <paramref name="branch"/>:
    /// null when the file is not on that branch.
    /// </summary>
    Task<string?> TryReadFileAsync(string path, BranchName branch, CancellationToken cancellationToken);

    /// <summary>
    /// <see cref="ListDirectoryAsync(string, CancellationToken)"/> on <paramref name="branch"/>:
    /// empty when the path is not on that branch.
    /// </summary>
    Task<IReadOnlyList<string>> ListDirectoryAsync(
        string path, BranchName branch, CancellationToken cancellationToken);

    /// <summary>
    /// 2026-11-23-c81ea: every FILE under <paramref name="path"/> on <paramref name="branch"/>, at any depth, as
    /// repo-relative paths; empty when the folder is not there. At most <paramref name="maxFiles"/> of them, and
    /// <see cref="RepoFileListing.Truncated"/> when more exist or the host cut its answer. The read is scoped to the
    /// folder, never the whole branch. Auth and transport errors propagate.
    /// </summary>
    Task<RepoFileListing> ListFilesUnderAsync(
        string path, BranchName branch, int maxFiles, CancellationToken cancellationToken);

    /// <summary>
    /// 2026-11-23-c81ea: the branch the default-branch reads use — the repository's own default where the host
    /// names one, else the configured branch, else 'main'.
    /// </summary>
    Task<BranchName> GetDefaultBranchAsync(CancellationToken cancellationToken);
}
