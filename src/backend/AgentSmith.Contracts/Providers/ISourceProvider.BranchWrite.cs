using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-09-22-b6ad: putting files on a branch WITHOUT a checkout — the one thing the contract's
/// other checkout-free members could not do. They read: one file and one directory listing, both
/// on the default branch. Everything the contract writes is a pull request, and creating a branch
/// was a git push run as a sandbox step.
/// <para>
/// A PARTIAL of its own file for the reason the targeting members are one: this is a question of
/// its own, and the contract file is at the length the coding principles allow.
/// </para>
/// <para>
/// It exists because FILING writes the approved specification onto the ticket branch as it files,
/// and filing holds no pipeline, no sandbox and no clone. Standing up a container and a full clone
/// to commit two kinds of small text file is the whole run's machinery borrowed for a commit, and
/// it would make filing wait on a sandbox pool.
/// </para>
/// </summary>
public partial interface ISourceProvider
{
    /// <summary>
    /// Puts <paramref name="files"/> on <paramref name="branch"/> in ONE commit, creating the
    /// branch at the default branch's head when it does not exist and committing on its head when
    /// it does. Never re-cuts and never forces: an existing ref is committed ONTO, because the
    /// run's own branch push already refuses an existing ref rather than overwriting it and its
    /// checkout already takes an existing branch as it stands.
    /// <para>
    /// Answers the commit sha, or the reason there is none. Never throws: a refused write is a
    /// normal answer here, because the caller has already created a ticket and a retry would file
    /// a second one.
    /// </para>
    /// </summary>
    /// <param name="message">The commit message.</param>
    Task<BranchWriteResult> WriteFilesToBranchAsync(
        BranchName branch, IReadOnlyList<RepoFile> files, string message,
        CancellationToken cancellationToken);
}
