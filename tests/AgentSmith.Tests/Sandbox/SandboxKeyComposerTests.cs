using AgentSmith.Application.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0322b: multi-group keys carry the group's representative context name
/// (unique per repo by directory construction) instead of lang+resource slugs.
/// The old slugs showed what was identical across groups (lang, size) and hid
/// the differing image, so distinct groups collided into the coordinator's
/// numeric "-2" backstop. Single-group keys are unchanged from p0180.
/// </summary>
public sealed class SandboxKeyComposerTests
{
    [Fact]
    public void ComposeForGroup_SingleGroup_Unchanged()
    {
        SandboxKeyComposer.ComposeForGroup(repoCount: 1, "repo", repoGroupCount: 1, "api")
            .Should().Be("default");
        SandboxKeyComposer.ComposeForGroup(repoCount: 2, "repo", repoGroupCount: 1, "api")
            .Should().Be("repo");
    }

    [Fact]
    public void ComposeForGroup_MultiGroup_UsesContextName()
    {
        SandboxKeyComposer.ComposeForGroup(repoCount: 1, "repo", repoGroupCount: 2, "api")
            .Should().Be("api");
        SandboxKeyComposer.ComposeForGroup(repoCount: 3, "worker", repoGroupCount: 2, "api")
            .Should().Be("worker-api");
    }

    [Fact]
    public void ComposeForGroup_MultiGroup_SanitizesContextName()
    {
        SandboxKeyComposer.ComposeForGroup(repoCount: 1, "repo", repoGroupCount: 2, "Api.V1")
            .Should().Be("api-v1");
        SandboxKeyComposer.ComposeForGroup(repoCount: 2, "repo", repoGroupCount: 2, "Client Gen")
            .Should().Be("repo-client-gen");
    }
}
