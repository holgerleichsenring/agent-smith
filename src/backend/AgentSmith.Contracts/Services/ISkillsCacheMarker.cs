namespace AgentSmith.Contracts.Services;

/// <summary>
/// Reads and writes the <c>.pulled</c> marker that records which catalog version
/// is currently extracted in a given cache directory.
/// </summary>
public interface ISkillsCacheMarker
{
    /// <summary>
    /// Returns the version string from <c>{cacheDir}/.pulled</c>, or null if the
    /// marker is missing or unreadable.
    /// </summary>
    string? Read(string cacheDir);

    /// <summary>Writes <c>{cacheDir}/.pulled</c> with the given version.</summary>
    void Write(string cacheDir, string version);
}
