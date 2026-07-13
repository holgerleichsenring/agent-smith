namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0325: the skills release tarball baked into the build. The MSBuild embed
/// step pins the version, verifies the SHA256, and embeds the tar.gz as an
/// assembly resource; this contract exposes it to the runtime so
/// <c>EmbeddedSourceHandler</c> can materialize the catalog without any
/// network access.
/// </summary>
public interface IEmbeddedSkillsCatalog
{
    /// <summary>Release tag of the embedded catalog (e.g. <c>v3.20.0</c>).</summary>
    string Version { get; }

    /// <summary>Opens the embedded gzip'd tarball for reading. Caller disposes.</summary>
    Stream Open();
}
