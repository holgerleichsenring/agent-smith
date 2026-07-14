using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Skills;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services.Skills;

/// <summary>
/// The resolver must NOT latch "resolve once per process" — it delegates to the
/// source handler on every call so a changed version pin or a wiped on-disk
/// cache self-heals (the handler is idempotent + version-aware and skips the
/// pull when the marker matches). Regression for the cold-cache deadlock and
/// stale-pin bug, where a process-wide _resolved flag short-circuited the
/// handler's self-healing for the whole process lifetime.
/// </summary>
public sealed class SkillsCatalogResolverTests
{
    private sealed class CountingHandler : ISkillsSourceHandler
    {
        public int Calls { get; private set; }
        public string Root { get; init; } = "/catalog";
        public SkillsSourceMode Mode => SkillsSourceMode.Default;

        public Task<CatalogResolution> ResolveAsync(SkillsConfig config, CancellationToken cancellationToken)
        {
            Calls++;
            // FromCache flips true after the first call so the resolver-pass-through
            // tests can assert the per-call binding it surfaces.
            return Task.FromResult(
                new CatalogResolution(Root, config.Version ?? "v1", Mode, "https://example/release", Calls > 1));
        }
    }

    private static SkillsConfig DefaultConfig() =>
        new() { Source = SkillsSourceMode.Default, Version = "v1", CacheDir = "/cache" };

    [Fact]
    public async Task EnsureResolvedAsync_DelegatesToHandler_OnEveryCall()
    {
        var handler = new CountingHandler();
        var sut = new SkillsCatalogResolver(
            new[] { handler }, new SkillsCatalogPath(),
            NullLogger<SkillsCatalogResolver>.Instance);

        await sut.EnsureResolvedAsync(DefaultConfig(), CancellationToken.None);
        await sut.EnsureResolvedAsync(DefaultConfig(), CancellationToken.None);
        await sut.EnsureResolvedAsync(DefaultConfig(), CancellationToken.None);

        handler.Calls.Should().Be(3,
            "the resolver must re-check on every call so a changed pin or wiped cache self-heals — no process-wide latch");
    }

    [Fact]
    public async Task EnsureResolvedAsync_PublishesHandlerRoot_ToCatalogPath()
    {
        var handler = new CountingHandler { Root = "/resolved/here" };
        var path = new SkillsCatalogPath();
        var sut = new SkillsCatalogResolver(
            new[] { handler }, path, NullLogger<SkillsCatalogResolver>.Instance);

        await sut.EnsureResolvedAsync(DefaultConfig(), CancellationToken.None);

        path.Root.Should().Be("/resolved/here");
    }

    [Fact]
    public async Task EnsureResolvedAsync_ReturnsHandlerBinding_ToCaller()
    {
        var handler = new CountingHandler { Root = "/resolved/here" };
        var sut = new SkillsCatalogResolver(
            new[] { handler }, new SkillsCatalogPath(),
            NullLogger<SkillsCatalogResolver>.Instance);

        var resolution = await sut.EnsureResolvedAsync(DefaultConfig(), CancellationToken.None);

        resolution.Root.Should().Be("/resolved/here");
        resolution.Version.Should().Be("v1");
        resolution.Source.Should().Be(SkillsSourceMode.Default);
        resolution.SourceUrl.Should().Be("https://example/release");
    }

    [Fact]
    public async Task SkillsCatalogResolver_FreshPull_SetsFromCacheFalseAndSourceUrl()
    {
        var handler = new CountingHandler();
        var sut = new SkillsCatalogResolver(
            new[] { handler }, new SkillsCatalogPath(),
            NullLogger<SkillsCatalogResolver>.Instance);

        var resolution = await sut.EnsureResolvedAsync(DefaultConfig(), CancellationToken.None);

        resolution.FromCache.Should().BeFalse("the first resolve is a fresh pull");
        resolution.SourceUrl.Should().Be("https://example/release");
    }

    [Fact]
    public async Task SkillsCatalogResolver_SecondRun_SetsFromCacheTrue()
    {
        var handler = new CountingHandler();
        var sut = new SkillsCatalogResolver(
            new[] { handler }, new SkillsCatalogPath(),
            NullLogger<SkillsCatalogResolver>.Instance);

        await sut.EnsureResolvedAsync(DefaultConfig(), CancellationToken.None);
        var second = await sut.EnsureResolvedAsync(DefaultConfig(), CancellationToken.None);

        second.FromCache.Should().BeTrue("the warm cache is re-used on the second run");
    }

    // p0325: explicit config wins over the embedded default. A path override
    // (the operator's skills-development workflow) dispatches to the REAL
    // PathSourceHandler even though the embedded handler is registered — the
    // embedded catalog is only the default when nothing is configured.
    [Fact]
    public async Task SkillsCatalogResolver_ExplicitPathConfig_OverridesEmbedded()
    {
        var catalogDir = Path.Combine(Path.GetTempPath(), $"agentsmith-path-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(catalogDir, "skills"));
        try
        {
            var handlers = new ISkillsSourceHandler[]
            {
                new EmbeddedSourceHandler(
                    new EmbeddedSkillsCatalog(),
                    new CatalogTarballExtractor(NullLogger<CatalogTarballExtractor>.Instance),
                    new SkillsCacheMarker(NullLogger<SkillsCacheMarker>.Instance),
                    NullLogger<EmbeddedSourceHandler>.Instance),
                new PathSourceHandler(NullLogger<PathSourceHandler>.Instance),
            };
            var sut = new SkillsCatalogResolver(
                handlers, new SkillsCatalogPath(), NullLogger<SkillsCatalogResolver>.Instance);
            var config = new SkillsConfig { Source = SkillsSourceMode.Path, Path = catalogDir, CacheDir = "/unused" };

            var resolution = await sut.EnsureResolvedAsync(config, CancellationToken.None);

            resolution.Source.Should().Be(SkillsSourceMode.Path);
            resolution.Root.Should().Be(catalogDir, "the mounted working tree wins over the embedded catalog");
        }
        finally
        {
            Directory.Delete(catalogDir, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureResolvedAsync_NoHandlerForSource_Throws()
    {
        var sut = new SkillsCatalogResolver(
            Array.Empty<ISkillsSourceHandler>(), new SkillsCatalogPath(),
            NullLogger<SkillsCatalogResolver>.Instance);

        var act = () => sut.EnsureResolvedAsync(DefaultConfig(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No SkillsSourceHandler*");
    }
}
