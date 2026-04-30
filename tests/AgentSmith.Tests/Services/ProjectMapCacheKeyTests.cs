using AgentSmith.Application.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class ProjectMapCacheKeyTests : IDisposable
{
    private readonly string _root;

    public ProjectMapCacheKeyTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"cachekey-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Compute_NoManifests_ReturnsEmpty()
    {
        File.WriteAllText(Path.Combine(_root, "README.md"), "no manifests here");
        ProjectMapCacheKey.Compute(_root).Should().BeEmpty();
    }

    [Fact]
    public void Compute_SameCsprojContent_SameHash()
    {
        var path = Path.Combine(_root, "MyApp.csproj");
        File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        var first = ProjectMapCacheKey.Compute(_root);
        var second = ProjectMapCacheKey.Compute(_root);

        first.Should().NotBeEmpty();
        first.Should().Be(second);
    }

    [Fact]
    public void Compute_NewDependencyAdded_NewHash()
    {
        var path = Path.Combine(_root, "MyApp.csproj");
        File.WriteAllText(path, "<Project></Project>");
        var before = ProjectMapCacheKey.Compute(_root);

        File.WriteAllText(path, "<Project><PackageReference Include=\"Foo\" /></Project>");
        var after = ProjectMapCacheKey.Compute(_root);

        after.Should().NotBe(before);
    }

    [Fact]
    public void Compute_NonManifestFileChange_SameHash()
    {
        File.WriteAllText(Path.Combine(_root, "MyApp.csproj"), "<Project></Project>");
        var before = ProjectMapCacheKey.Compute(_root);

        File.WriteAllText(Path.Combine(_root, "README.md"), "non-manifest changes don't invalidate");
        var after = ProjectMapCacheKey.Compute(_root);

        after.Should().Be(before);
    }

    [Fact]
    public void Compute_BinObjFolders_NotIncluded()
    {
        File.WriteAllText(Path.Combine(_root, "MyApp.csproj"), "<Project></Project>");
        var before = ProjectMapCacheKey.Compute(_root);

        var binDir = Path.Combine(_root, "bin");
        Directory.CreateDirectory(binDir);
        File.WriteAllText(Path.Combine(binDir, "Other.csproj"),
            "<Project>shouldn't affect hash</Project>");

        var after = ProjectMapCacheKey.Compute(_root);
        after.Should().Be(before);
    }
}
