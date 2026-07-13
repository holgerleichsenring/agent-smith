namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0325: extracts a gzip'd skill-catalog tarball into a destination directory
/// atomically (staging dir + swap), so a crashed extraction never leaves a
/// half-written catalog behind. Shared by the download path
/// (<c>SkillsRepositoryClient</c>) and the embedded path
/// (<c>EmbeddedSourceHandler</c>).
/// </summary>
public interface ICatalogTarballExtractor
{
    /// <summary>
    /// Extracts <paramref name="tarGzStream"/> into <paramref name="destinationDir"/>,
    /// replacing any existing contents in one atomic directory swap.
    /// </summary>
    void Extract(Stream tarGzStream, string destinationDir);
}
