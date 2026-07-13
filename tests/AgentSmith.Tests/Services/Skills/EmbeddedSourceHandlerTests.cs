using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Skills;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Skills;

/// <summary>
/// p0325: the embedded skills release must materialize to the catalog root
/// with ZERO network access — the whole point of shipping skills inside the
/// release. These tests run the REAL embedded resource (the tarball baked into
/// AgentSmith.Infrastructure.Core at build time) through the real extractor
/// and marker; nothing here can open a socket.
/// </summary>
public sealed class EmbeddedSourceHandlerTests : IDisposable
{
    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(),
        $"agentsmith-embedded-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, recursive: true);
    }

    private static EmbeddedSourceHandler CreateHandler() => new(
        new EmbeddedSkillsCatalog(),
        new CatalogTarballExtractor(NullLogger<CatalogTarballExtractor>.Instance),
        new SkillsCacheMarker(NullLogger<SkillsCacheMarker>.Instance),
        NullLogger<EmbeddedSourceHandler>.Instance);

    private SkillsConfig Config() => new() { Source = SkillsSourceMode.Embedded, CacheDir = _cacheDir };

    [Fact]
    public async Task EmbeddedSourceHandler_FreshInstall_MaterializesCatalogOffline()
    {
        var resolution = await CreateHandler().ResolveAsync(Config(), CancellationToken.None);

        resolution.Root.Should().Be(_cacheDir);
        resolution.Source.Should().Be(SkillsSourceMode.Embedded);
        resolution.FromCache.Should().BeFalse("a fresh install extracts the embedded tarball");
        resolution.Version.Should().MatchRegex(@"^v\d+\.\d+\.\d+$",
            "the version is the pinned release tag, not a directory name");
        resolution.SourceUrl.Should().StartWith("embedded://agentsmith-skills/");
        Directory.Exists(Path.Combine(_cacheDir, "skills", "_masters"))
            .Should().BeTrue("masters must be loadable from the materialized catalog root");
    }

    [Fact]
    public async Task EmbeddedSourceHandler_SecondResolve_ReusesMaterializedCatalog()
    {
        var handler = CreateHandler();
        await handler.ResolveAsync(Config(), CancellationToken.None);

        var second = await handler.ResolveAsync(Config(), CancellationToken.None);

        second.FromCache.Should().BeTrue("the marker matches the embedded version — no re-extraction");
    }

    [Fact]
    public async Task EmbeddedSourceHandler_VersionDrift_ReExtracts()
    {
        var handler = CreateHandler();
        await handler.ResolveAsync(Config(), CancellationToken.None);
        // Simulate a catalog materialized by an OLDER binary: the marker holds
        // a version that is not the one embedded in this build.
        File.WriteAllText(Path.Combine(_cacheDir, ".pulled"), "v0.0.1");

        var resolution = await handler.ResolveAsync(Config(), CancellationToken.None);

        resolution.FromCache.Should().BeFalse(
            "an upgraded binary must replace the stale catalog with its own embedded release");
    }
}
