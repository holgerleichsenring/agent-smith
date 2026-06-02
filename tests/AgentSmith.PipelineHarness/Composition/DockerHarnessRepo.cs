using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199b: builds the RepoConnection that the docker-tier harness session
/// injects into PipelineRunner. Type=GitHub forces CheckoutSourceHandler
/// down the real `git clone` path inside the sandbox; Url points at the
/// per-test bare repo bind-mounted into the container.
/// </summary>
internal static class DockerHarnessRepo
{
    public static RepoConnection For(DockerHarnessSession session) => new()
    {
        Name = "primary",
        Type = RepoType.GitHub,
        Url = session.InSandboxBareUrl,
        Auth = "gh_token",
        DefaultBranch = "main",
    };
}
