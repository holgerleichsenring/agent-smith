namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-24-5ee0: resolves the open review threads this token opened whose first comment starts with a marker — a notice
/// agent-smith posted that has stopped being true. Implemented by a host whose plain pull-request comment opens a thread a
/// branch policy can wait on (Azure Repos). GitLab posts such a notice as a plain MR note, which is not resolvable and never
/// blocks "all threads resolved". Reached by casting the repo's provider.
/// <para>2026-11-25-7a92d: GitHub cannot resolve an issue comment either and none blocks a merge; its provider hides the
/// token's marked comments as resolved instead, so the notice goes quiet as Azure's resolved thread does.</para>
/// </summary>
public interface IPrMarkedThreadResolver
{
    /// <summary>Resolves (on GitHub: hides) every such thread on the pull request; returns how many.</summary>
    Task<int> ResolveMarkedThreadsAsync(string prIdentifier, string markerPrefix, CancellationToken cancellationToken);
}
