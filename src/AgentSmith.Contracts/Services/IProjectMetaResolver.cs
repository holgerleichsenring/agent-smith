namespace AgentSmith.Contracts.Services;

/// <summary>
/// Locates the .agentsmith/ metadata directory for a target project.
/// Searches under the supplied source path (the checked-out target),
/// not the AgentSmith working directory. For mono-repo layouts the
/// first .agentsmith/ encountered depth-first in lexical order wins.
/// </summary>
public interface IProjectMetaResolver
{
    /// <summary>
    /// Returns the absolute path to a .agentsmith/ directory under
    /// <paramref name="sourcePath"/>, or null if none is found.
    /// </summary>
    string? Resolve(string sourcePath);
}
