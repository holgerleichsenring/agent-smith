using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199b: source-provider factory that hands out a single
/// LocalGitSourceProvider bound to one DockerHarnessSession. Per-test
/// session ensures the bare repo path is isolated per test run.
/// </summary>
internal sealed class LocalGitSourceProviderFactory(DockerHarnessSession session) : ISourceProviderFactory
{
    public ISourceProvider Create(RepoConnection config) => new LocalGitSourceProvider(session);
}
