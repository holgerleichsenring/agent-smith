using AgentSmith.Application.Services;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

public sealed class ProjectMapCacheKeyTests
{
    [Fact]
    public async Task ComputeAsync_NoManifests_ReturnsEmpty()
    {
        var reader = NewReader(new Dictionary<string, string?>
        {
            ["/work/README.md"] = "no manifests here"
        });

        var hash = await ProjectMapCacheKey.ComputeAsync(reader.Object, "/work", CancellationToken.None);

        hash.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputeAsync_SameCsprojContent_SameHash()
    {
        var reader = NewReader(new Dictionary<string, string?>
        {
            ["/work/MyApp.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>"
        });

        var first = await ProjectMapCacheKey.ComputeAsync(reader.Object, "/work", CancellationToken.None);
        var second = await ProjectMapCacheKey.ComputeAsync(reader.Object, "/work", CancellationToken.None);

        first.Should().NotBeEmpty();
        first.Should().Be(second);
    }

    [Fact]
    public async Task ComputeAsync_NewDependencyAdded_NewHash()
    {
        var beforeReader = NewReader(new Dictionary<string, string?>
        {
            ["/work/MyApp.csproj"] = "<Project></Project>"
        });
        var afterReader = NewReader(new Dictionary<string, string?>
        {
            ["/work/MyApp.csproj"] = "<Project><PackageReference Include=\"Foo\" /></Project>"
        });

        var before = await ProjectMapCacheKey.ComputeAsync(beforeReader.Object, "/work", CancellationToken.None);
        var after = await ProjectMapCacheKey.ComputeAsync(afterReader.Object, "/work", CancellationToken.None);

        after.Should().NotBe(before);
    }

    [Fact]
    public async Task ComputeAsync_NonManifestFileChange_SameHash()
    {
        var beforeReader = NewReader(new Dictionary<string, string?>
        {
            ["/work/MyApp.csproj"] = "<Project></Project>"
        });
        var afterReader = NewReader(new Dictionary<string, string?>
        {
            ["/work/MyApp.csproj"] = "<Project></Project>",
            ["/work/README.md"] = "non-manifest changes don't invalidate"
        });

        var before = await ProjectMapCacheKey.ComputeAsync(beforeReader.Object, "/work", CancellationToken.None);
        var after = await ProjectMapCacheKey.ComputeAsync(afterReader.Object, "/work", CancellationToken.None);

        after.Should().Be(before);
    }

    [Fact]
    public async Task ComputeAsync_BinObjFolders_NotIncluded()
    {
        var beforeReader = NewReader(new Dictionary<string, string?>
        {
            ["/work/MyApp.csproj"] = "<Project></Project>"
        });
        var afterReader = NewReader(new Dictionary<string, string?>
        {
            ["/work/MyApp.csproj"] = "<Project></Project>",
            ["/work/bin/Other.csproj"] = "<Project>shouldn't affect hash</Project>"
        });

        var before = await ProjectMapCacheKey.ComputeAsync(beforeReader.Object, "/work", CancellationToken.None);
        var after = await ProjectMapCacheKey.ComputeAsync(afterReader.Object, "/work", CancellationToken.None);

        after.Should().Be(before);
    }

    private static Mock<ISandboxFileReader> NewReader(Dictionary<string, string?> entries)
    {
        var reader = new Mock<ISandboxFileReader>();
        reader.Setup(r => r.ListAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries.Keys.ToList());
        reader.Setup(r => r.TryReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((p, _) =>
                Task.FromResult(entries.TryGetValue(p, out var c) ? c : null));
        return reader;
    }
}
