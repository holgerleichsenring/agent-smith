using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Provides git operations for a source repository.
/// </summary>
public interface ISourceProvider : ITypedProvider
{
    /// <summary>
    /// Read-only connectivity probe: performs the cheapest authenticated round-trip
    /// the provider supports and reports whether the credentials work and the remote
    /// is reachable. Never writes. Implementations must not throw — transport/auth
    /// failures are captured in <see cref="ConnectionProbeResult.Error"/>.
    /// </summary>
    Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken);

    Task<Repository> CheckoutAsync(BranchName? branch, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a pull request. When <paramref name="linkedTicketId"/> is set,
    /// the provider also associates the ticket with the PR using the platform's
    /// native mechanism — AzDO attaches it as a Work Item Ref, GitHub/GitLab
    /// add a "Closes #N"-style auto-link to the description. Providers without
    /// a sensible link mechanism ignore the parameter.
    /// When <paramref name="isDraft"/> is set, the PR is opened as a draft /
    /// work-in-progress so it is visible for review but not mergeable — used for
    /// a verification-red run. Providers without a draft concept ignore it.
    /// </summary>
    Task<string> CreatePullRequestAsync(
        Repository repository,
        string title,
        string description,
        CancellationToken cancellationToken,
        TicketId? linkedTicketId = null,
        bool isDraft = false);

    /// <summary>
    /// p0390: the URL of the OPEN pull request whose source branch is
    /// <paramref name="repository"/>'s current branch, or null when there is none.
    /// CommitAndPR is find-or-create because a run now opens its draft PR early —
    /// at the work-spec commit, so a reviewer has something to edit while the run
    /// is still working — and a second unconditional create on the same branch
    /// would throw into the handler's catch and report the whole PR step failed.
    /// Never throws: a provider that cannot search returns null and the caller
    /// falls back to creating, which is the pre-p0390 behaviour.
    /// </summary>
    Task<string?> FindOpenPullRequestAsync(Repository repository, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a file from the repository's default branch without a full clone.
    /// Returns null when the file does not exist (404 / file-not-found across
    /// the four implementations). Auth + server errors propagate so the caller
    /// can distinguish "no bootstrap yet" from "I can't talk to the remote".
    /// Used by SandboxLanguageResolver (p0135) to peek at
    /// .agentsmith/context.yaml before the sandbox is created.
    /// </summary>
    Task<string?> TryReadFileAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Lists immediate children (files + sub-directories, no recursion) at the
    /// given repo-relative path on the default branch, without a full clone.
    /// Returns names only — caller composes full paths. Returns empty list
    /// when the path does not exist on the remote; auth + transport errors
    /// propagate. Used by SandboxLanguageResolver (p0161) to discover
    /// .agentsmith/contexts/* sub-dirs before the sandbox is created so the
    /// orchestrator can spawn one sandbox per discovered context with the
    /// right toolchain image per context.
    /// </summary>
    Task<IReadOnlyList<string>> ListDirectoryAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the body / description of an already-opened pull request. Used by
    /// PrCrossLinkHandler in p0158c's pass-2 to replace the sibling-PRs marker
    /// with the actual sibling URL list once every per-repo PR has been opened.
    /// Returns true on success (HTTP 2xx / no throw); false on any non-success
    /// or exception (logged at WARN). prUrl is the web URL returned by the
    /// prior CreatePullRequestAsync call; each impl parses it to recover the
    /// platform-specific identifier.
    /// </summary>
    Task<bool> UpdatePullRequestBodyAsync(
        string prUrl, string newBody, CancellationToken cancellationToken);

    /// <summary>
    /// p0393a: takes an already-opened pull request out of draft.
    /// <para>
    /// A run opens its PR as a draft at the spec commit, before any phase has been
    /// verified, and a sequence that stops mid-way must STAY a draft: a half-migrated
    /// repository behind a mergeable PR is the failure the stop exists to prevent, in a
    /// form that looks finished. Promotion is therefore a deliberate act at the end of a
    /// complete, green run — without this method "still a draft" would carry no
    /// information, because nothing would ever leave draft.
    /// </para>
    /// Returns true when the pull request is (now) ready for review; false on any
    /// non-success, and for providers without a draft concept, where nothing has to
    /// happen for it to be mergeable.
    /// </summary>
    Task<bool> MarkPullRequestReadyAsync(string prUrl, CancellationToken cancellationToken);
}
