using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Locates the .agentsmith/ metadata directory for a target project. Routes
/// directory listings through ISandboxFileReader so the lookup happens against
/// the sandbox filesystem (i.e. /work after p0117b). Repo-root first, then a
/// depth-first lexical descent for monorepo layouts. First hit wins.
/// </summary>
public interface IProjectMetaResolver
{
    /// <summary>
    /// Returns the absolute path (under <paramref name="sourcePath"/>) of a
    /// .agentsmith/ directory, or null if none is found.
    /// </summary>
    Task<string?> ResolveAsync(
        ISandboxFileReader reader, string sourcePath, CancellationToken cancellationToken);
}
