using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class ProjectMetaResolverAsyncTests
{
    [Fact]
    public async Task ResolveAsync_RootHasMetaDir_ReturnsRootCandidate()
    {
        var reader = Reader(new[]
        {
            "/work/.agentsmith",
            "/work/src",
            "/work/README.md"
        });

        var result = await new ProjectMetaResolver().ResolveAsync(
            reader.Object, "/work", CancellationToken.None);

        result.Should().Be("/work/.agentsmith");
    }

    [Fact]
    public async Task ResolveAsync_MonorepoSubpackage_ReturnsFirstHit()
    {
        var reader = Reader(new[]
        {
            "/work/services",
            "/work/services/api",
            "/work/services/api/.agentsmith",
            "/work/services/web",
            "/work/services/web/.agentsmith"
        });

        var result = await new ProjectMetaResolver().ResolveAsync(
            reader.Object, "/work", CancellationToken.None);

        result.Should().Be("/work/services/api/.agentsmith");
    }

    [Fact]
    public async Task ResolveAsync_MetaInsideNodeModules_IsIgnored()
    {
        var reader = Reader(new[]
        {
            "/work/node_modules",
            "/work/node_modules/foo",
            "/work/node_modules/foo/.agentsmith",
            "/work/src",
            "/work/src/app",
            "/work/src/app/.agentsmith"
        });

        var result = await new ProjectMetaResolver().ResolveAsync(
            reader.Object, "/work", CancellationToken.None);

        result.Should().Be("/work/src/app/.agentsmith");
    }

    [Fact]
    public async Task ResolveAsync_NoMetaDir_ReturnsNull()
    {
        var reader = Reader(new[]
        {
            "/work/src",
            "/work/README.md"
        });

        var result = await new ProjectMetaResolver().ResolveAsync(
            reader.Object, "/work", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_EmptySourcePath_ReturnsNull()
    {
        var reader = new Mock<ISandboxFileReader>();

        var result = await new ProjectMetaResolver().ResolveAsync(
            reader.Object, "", CancellationToken.None);

        result.Should().BeNull();
    }

    private static Mock<ISandboxFileReader> Reader(IReadOnlyList<string> entries)
    {
        var mock = new Mock<ISandboxFileReader>();
        mock.Setup(r => r.ListAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        return mock;
    }
}
