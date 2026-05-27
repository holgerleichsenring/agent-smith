using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Providers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Loop;

public sealed class PathReadGuardTests
{
    private const string RepoRoot = "/work";

    private static PathReadGuard Build(IGitIgnoreResolver? gitIgnore = null)
    {
        var resolver = gitIgnore ?? CreateNoIgnoreResolver();
        return new PathReadGuard(resolver);
    }

    private static IGitIgnoreResolver CreateNoIgnoreResolver()
    {
        var mock = new Mock<IGitIgnoreResolver>();
        mock.Setup(g => g.IsIgnored(It.IsAny<string>(), It.IsAny<string>())).Returns(false);
        return mock.Object;
    }

    [Fact]
    public void AssertReadable_PathInsideRepo_ReturnsOk()
    {
        var guard = Build();

        var result = guard.AssertReadable("/work/src/main.cs", RepoRoot);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AssertReadable_PathOutsideRepo_ReturnsErrorOutsideRepo()
    {
        var guard = Build();

        var result = guard.AssertReadable("/etc/passwd", RepoRoot);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(GuardErrorKind.OutsideRepo);
    }

    [Fact]
    public void AssertReadable_PathInGitIgnore_ReturnsErrorIgnored()
    {
        var gitIgnore = new Mock<IGitIgnoreResolver>();
        gitIgnore.Setup(g => g.IsIgnored("/work/node_modules/foo", RepoRoot)).Returns(true);
        var guard = Build(gitIgnore.Object);

        var result = guard.AssertReadable("/work/node_modules/foo", RepoRoot);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(GuardErrorKind.GitIgnored);
    }

    [Fact]
    public void AssertReadable_PathInDotGit_ReturnsErrorInDotGit()
    {
        var guard = Build();

        var result = guard.AssertReadable("/work/.git/HEAD", RepoRoot);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(GuardErrorKind.InDotGit);
    }

    [Fact]
    public void AssertReadable_RelativePathResolvesToInsideRepo_ReturnsOk()
    {
        var guard = Build();

        var result = guard.AssertReadable("src/main.cs", RepoRoot);

        result.IsSuccess.Should().BeTrue();
    }
}
