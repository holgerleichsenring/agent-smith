using AgentSmith.Application.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0268: groups are keyed by (image, resources), so two groups can share a
/// langSlug (same language, different size). The composer must keep their keys
/// distinct via the resource slug — otherwise the second sandbox is silently
/// dropped. Without a slug the p0180 behavior is unchanged.
/// </summary>
public sealed class SandboxKeyComposerTests
{
    [Fact]
    public void ComposeForGroup_SingleRepoSingleGroup_IsDefault()
    {
        SandboxKeyComposer.ComposeForGroup(repoCount: 1, "repo", repoGroupCount: 1, "csharp")
            .Should().Be("default");
    }

    [Fact]
    public void ComposeForGroup_NoResourceSlug_UnchangedFromP0180()
    {
        SandboxKeyComposer.ComposeForGroup(repoCount: 1, "repo", repoGroupCount: 2, "csharp")
            .Should().Be("csharp");
        SandboxKeyComposer.ComposeForGroup(repoCount: 2, "repo", repoGroupCount: 2, "csharp")
            .Should().Be("repo-csharp");
    }

    [Fact]
    public void ComposeForGroup_SameLangDifferentResources_ProducesDistinctKeys()
    {
        var heavy = SandboxKeyComposer.ComposeForGroup(1, "repo", repoGroupCount: 2, "csharp", "2-4gi");
        var light = SandboxKeyComposer.ComposeForGroup(1, "repo", repoGroupCount: 2, "csharp", "500m-512mi");

        heavy.Should().Be("csharp-2-4gi");
        light.Should().Be("csharp-500m-512mi");
        heavy.Should().NotBe(light);
    }

    [Fact]
    public void ComposeForGroup_MultiRepoSameLangDifferentResources_ProducesDistinctKeys()
    {
        var heavy = SandboxKeyComposer.ComposeForGroup(2, "repo", repoGroupCount: 2, "csharp", "2-4gi");
        var light = SandboxKeyComposer.ComposeForGroup(2, "repo", repoGroupCount: 2, "csharp", "500m-512mi");

        heavy.Should().Be("repo-csharp-2-4gi");
        light.Should().Be("repo-csharp-500m-512mi");
    }
}
