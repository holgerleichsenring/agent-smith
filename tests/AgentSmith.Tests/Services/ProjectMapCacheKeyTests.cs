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

        var hash = await ProjectMapCacheKey.ComputeAsync(reader.Object, "/work", null, CancellationToken.None);

        hash.Should().BeEmpty();
    }

    [Fact]
    public async Task ComputeAsync_SameCsprojContent_SameHash()
    {
        var reader = NewReader(new Dictionary<string, string?>
        {
            ["/work/MyApp.csproj"] = "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>"
        });

        var first = await ProjectMapCacheKey.ComputeAsync(reader.Object, "/work", null, CancellationToken.None);
        var second = await ProjectMapCacheKey.ComputeAsync(reader.Object, "/work", null, CancellationToken.None);

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

        var before = await ProjectMapCacheKey.ComputeAsync(beforeReader.Object, "/work", null, CancellationToken.None);
        var after = await ProjectMapCacheKey.ComputeAsync(afterReader.Object, "/work", null, CancellationToken.None);

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

        var before = await ProjectMapCacheKey.ComputeAsync(beforeReader.Object, "/work", null, CancellationToken.None);
        var after = await ProjectMapCacheKey.ComputeAsync(afterReader.Object, "/work", null, CancellationToken.None);

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

        var before = await ProjectMapCacheKey.ComputeAsync(beforeReader.Object, "/work", null, CancellationToken.None);
        var after = await ProjectMapCacheKey.ComputeAsync(afterReader.Object, "/work", null, CancellationToken.None);

        after.Should().Be(before);
    }

    // p0240: a source-only commit leaves dependency manifests byte-identical;
    // the HEAD SHA is what must change the key so a stale ProjectMap is not
    // served. These pin that invalidation.
    [Fact]
    public async Task ComputeAsync_HeadShaChanges_NewHash_EvenWithIdenticalManifests()
    {
        var reader = NewReader(new Dictionary<string, string?>
        {
            ["/work/MyApp.csproj"] = "<Project></Project>"
        });

        var before = await ProjectMapCacheKey.ComputeAsync(reader.Object, "/work", "aaaa1111", CancellationToken.None);
        var after = await ProjectMapCacheKey.ComputeAsync(reader.Object, "/work", "bbbb2222", CancellationToken.None);

        before.Should().NotBeEmpty();
        after.Should().NotBe(before);
    }

    [Fact]
    public async Task ComputeAsync_SameHeadSha_SameHash()
    {
        var reader = NewReader(new Dictionary<string, string?>
        {
            ["/work/MyApp.csproj"] = "<Project></Project>"
        });

        var first = await ProjectMapCacheKey.ComputeAsync(reader.Object, "/work", "aaaa1111", CancellationToken.None);
        var second = await ProjectMapCacheKey.ComputeAsync(reader.Object, "/work", "aaaa1111", CancellationToken.None);

        first.Should().Be(second);
    }

    [Fact]
    public async Task ComputeAsync_NoManifestsButHeadSha_NotEmpty()
    {
        var reader = NewReader(new Dictionary<string, string?>
        {
            ["/work/README.md"] = "no manifests, but a real git repo has a HEAD"
        });

        var hash = await ProjectMapCacheKey.ComputeAsync(reader.Object, "/work", "cccc3333", CancellationToken.None);

        hash.Should().NotBeEmpty();
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
