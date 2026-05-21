using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Provides git operations for a source repository.
/// </summary>
public interface ISourceProvider : ITypedProvider
{
    Task<Repository> CheckoutAsync(BranchName? branch, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a pull request. When <paramref name="linkedTicketId"/> is set,
    /// the provider also associates the ticket with the PR using the platform's
    /// native mechanism — AzDO attaches it as a Work Item Ref, GitHub/GitLab
    /// add a "Closes #N"-style auto-link to the description. Providers without
    /// a sensible link mechanism ignore the parameter.
    /// </summary>
    Task<string> CreatePullRequestAsync(
        Repository repository,
        string title,
        string description,
        CancellationToken cancellationToken,
        TicketId? linkedTicketId = null);

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
}
