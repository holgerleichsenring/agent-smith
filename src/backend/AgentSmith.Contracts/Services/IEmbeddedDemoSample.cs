namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0326: the demo sample project tarball baked into the build. The
/// PackDemoSampleProject MSBuild step packs the checked-in sources under
/// Resources/DemoSampleProject/ into a tar.gz and embeds it, so
/// `agent-smith demo` can materialize a ready-to-run buggy workspace
/// without any network access.
/// </summary>
public interface IEmbeddedDemoSample
{
    /// <summary>Opens the embedded gzip'd tarball for reading. Caller disposes.</summary>
    Stream Open();
}
