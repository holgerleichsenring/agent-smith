using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0407: the package cache is an ecosystem-neutral table, and Docker backs each row
/// with a volume that outlives the sandbox. These pin both halves: the catalog stays
/// data (no package manager is special), and the toggle really is off-safe.
/// </summary>
public sealed class PackageCacheTests
{
    [Fact]
    public void Catalog_EveryEntry_MountsUnderTheRootAndPointsItsEnvIntoItsOwnMount()
    {
        PackageCacheCatalog.All.Should().NotBeEmpty();
        foreach (var cache in PackageCacheCatalog.All)
        {
            cache.MountPath.Should().Be($"{PackageCacheCatalog.Root}/{cache.Ecosystem}");
            cache.Env.Should().NotBeEmpty("a cache nothing points at is not a cache");
            cache.Env.Values.Should().AllSatisfy(path =>
                path.Should().Match(p => p == cache.MountPath || p.StartsWith(cache.MountPath + "/"),
                    "an env var pointing outside its own mount would not be cached"));
        }
    }

    [Fact]
    public void Catalog_CoversTheDotnetEcosystem_WithBothNugetPaths()
    {
        var nuget = PackageCacheCatalog.All.Single(c => c.Ecosystem == "nuget");

        nuget.Env.Should().ContainKey("NUGET_PACKAGES");
        nuget.Env.Should().ContainKey("NUGET_HTTP_CACHE_PATH");
    }

    [Fact]
    public void Catalog_EcosystemsAreUnique_SoOneVolumeNamePerRow()
    {
        PackageCacheCatalog.All.Select(c => c.Ecosystem).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Volumes_CacheEnabled_BacksEveryCatalogEntryWithItsOwnNamedVolume()
    {
        var caches = BuildCaches(Mock.Of<IDockerClient>(), enabled: true);

        caches.Volumes.Should().HaveCount(PackageCacheCatalog.All.Count);
        caches.Volumes.Should().AllSatisfy(v => v.VolumeName.Should().Be($"agentsmith-pkgcache-{v.Cache.Ecosystem}"));
        caches.Volumes.Single(v => v.Cache.Ecosystem == "nuget").Bind
            .Should().Be($"agentsmith-pkgcache-nuget:{PackageCacheCatalog.Root}/nuget");
    }

    [Fact]
    public void Volumes_CacheDisabled_IsEmpty()
    {
        BuildCaches(Mock.Of<IDockerClient>(), enabled: false).Volumes.Should().BeEmpty();
    }

    [Fact]
    public void EnvAssignments_RenderEachEnvVarInDockerForm()
    {
        var volume = BuildCaches(Mock.Of<IDockerClient>(), enabled: true).Volumes
            .Single(v => v.Cache.Ecosystem == "nuget");

        volume.EnvAssignments.Should().Contain($"NUGET_PACKAGES={PackageCacheCatalog.Root}/nuget/packages");
    }

    [Fact]
    public async Task EnsureAsync_CacheEnabled_CreatesOneVolumePerCache()
    {
        var (docker, created) = BuildDockerMock();

        var volumes = await BuildCaches(docker, enabled: true).EnsureAsync(CancellationToken.None);

        created.Should().BeEquivalentTo(volumes.Select(v => v.VolumeName));
    }

    [Fact]
    public async Task EnsureAsync_CacheDisabled_CreatesNoVolume()
    {
        var (docker, created) = BuildDockerMock();

        var volumes = await BuildCaches(docker, enabled: false).EnsureAsync(CancellationToken.None);

        volumes.Should().BeEmpty();
        created.Should().BeEmpty();
    }

    private static DockerPackageCaches BuildCaches(IDockerClient docker, bool enabled) =>
        new(docker, new DockerSandboxOptions { PackageCacheEnabled = enabled },
            NullLogger<DockerPackageCaches>.Instance);

    private static (IDockerClient Docker, List<string> Created) BuildDockerMock()
    {
        var created = new List<string>();
        var volumes = new Mock<IVolumeOperations>();
        volumes.Setup(v => v.CreateAsync(It.IsAny<VolumesCreateParameters>(), It.IsAny<CancellationToken>()))
            .Callback<VolumesCreateParameters, CancellationToken>((p, _) => created.Add(p.Name))
            .ReturnsAsync(new VolumeResponse());
        var docker = new Mock<IDockerClient>();
        docker.SetupGet(d => d.Volumes).Returns(volumes.Object);
        return (docker.Object, created);
    }
}
