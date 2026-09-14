using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-09-13-a284: where an already-open pull request POINTS — reading its base and
/// moving it.
/// <para>
/// A PARTIAL of <see cref="ISourceProvider"/> rather than a second interface, because
/// every caller that can open a pull request must also be able to move one and would
/// otherwise have to type-test for it. A partial of its own file, because the contract
/// file is at the length the coding principles allow and this is a question of its own:
/// opening needs a repository, a branch and a default-branch lookup; retargeting needs
/// only the identifier recovered from the URL.
/// </para>
/// </summary>
public partial interface ISourceProvider
{
    /// <summary>
    /// The branch an already-open pull request targets, or null when the provider has no
    /// pull request server (Local) or could not read it — an unknown base is not a wrong
    /// one, so null means "do not move it".
    /// <para>
    /// The pull request an epic child actually gets is opened LONG BEFORE CommitAndPR: a
    /// draft at the spec commit, before the rung may even exist, and the find path only
    /// ever refreshed its body. Whether it sits on the right base is therefore a question
    /// somebody has to ask, and the caller holding the rung is the only one who can
    /// compare.
    /// </para>
    /// </summary>
    Task<string?> ReadPullRequestBaseAsync(string prUrl, CancellationToken cancellationToken);

    /// <summary>
    /// Moves an already-open pull request onto <paramref name="target"/> — GitHub patches
    /// <c>base</c>, GitLab <c>target_branch</c>, Azure Repos <c>targetRefName</c>, and a
    /// local repository, which has no pull request, no-ops. Returns true when the pull
    /// request is (now) based on the target; false on any non-success, which is logged and
    /// never thrown at a run that has already done its work.
    /// </summary>
    Task<bool> RetargetPullRequestAsync(
        string prUrl, BranchName target, CancellationToken cancellationToken);
}
