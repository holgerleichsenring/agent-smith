namespace AgentSmith.Contracts.Services;

/// <summary>
/// Downloads and verifies a skill catalog tarball into a target directory.
/// </summary>
public interface ISkillsRepositoryClient
{
    /// <summary>
    /// Resolves the release URL for the given version. Honours the
    /// <c>AGENTSMITH_SKILLS_REPOSITORY_URL</c> override if set.
    /// </summary>
    Uri ResolveReleaseUrl(string version);

    /// <summary>
    /// Downloads the tarball, verifies SHA256 if provided, and atomically
    /// extracts into <paramref name="outputDir"/>. The directory is replaced
    /// only after a successful download+verify+extract — partial failures leave
    /// any previous content untouched.
    /// </summary>
    Task PullAsync(
        Uri tarballUrl,
        string outputDir,
        string? expectedSha256,
        CancellationToken cancellationToken);
}
