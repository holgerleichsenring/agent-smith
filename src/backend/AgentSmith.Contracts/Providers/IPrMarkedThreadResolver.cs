namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-24-5ee0: resolves the open review threads this token opened whose first comment starts with a marker — a notice
/// agent-smith posted that has stopped being true. Implemented by a host whose plain pull-request comment opens a thread a
/// branch policy can wait on (Azure Repos). GitLab posts such a notice as a plain MR note, which is not resolvable and never
/// blocks "all threads resolved"; a GitHub issue comment is never resolvable. Reached by casting the repo's provider.
/// </summary>
public interface IPrMarkedThreadResolver
{
    /// <summary>Resolves every such thread on the pull request; returns how many were resolved.</summary>
    Task<int> ResolveMarkedThreadsAsync(string prIdentifier, string markerPrefix, CancellationToken cancellationToken);
}
