using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0285: builds a concrete <see cref="RepoConnection"/> for an EXACT (wildcard-free)
/// connection repo reference WITHOUT calling repo discovery. The git URL is derived
/// deterministically from the connection's type + org/project/owner/group + repo name,
/// matching the URL the discovery providers produce for the same repo.
/// </summary>
public interface IConnectionRepoUrlBuilder
{
    /// <summary>
    /// Builds the repo connection for <paramref name="repoName"/> under <paramref name="conn"/>.
    /// DefaultBranch precedence: <paramref name="branchOverride"/> -> connection default -> null
    /// (a null default is resolved to the repo's real default branch by the source provider at
    /// clone time, matching discovery behavior).
    /// </summary>
    RepoConnection Build(ResolvedConnection conn, string repoName, string? branchOverride);
}
