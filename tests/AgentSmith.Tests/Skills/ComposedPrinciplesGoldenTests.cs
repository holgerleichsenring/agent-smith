using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Skills;
using AgentSmith.Tests.Architecture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// 2026-09-15-d66f: the composed principles of every stack, pinned byte-for-byte.
/// <para>
/// The goldens were minted from the pin BEFORE the artefacts section existed, which is the
/// whole point: the catalog now makes <c>## Artefacts</c> mandatory on every delta, and the
/// composer inlines a delta file verbatim — so without stripping, this test fails for all
/// three stacks, including the two that declare nothing. It is the evidence for "every other
/// stack composes byte-identically to before", which the phase's first draft asserted with
/// nothing behind it.
/// </para>
/// <para>
/// It reads the EMBEDDED tarball, never a working checkout: a golden test that returns early
/// when the catalog is absent would pass by skipping and pin nothing.
/// </para>
/// </summary>
public sealed class ComposedPrinciplesGoldenTests : IDisposable
{
    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(),
        $"agentsmith-golden-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, recursive: true);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("rust")]
    [InlineData("typescript")]
    public async Task Compose_EveryStack_MatchesTheCheckedInGolden(string slug)
    {
        var composed = (await SourceAsync()).Compose(slug);

        composed.Should().NotBeNull($"the pinned catalog must carry a '{slug}' delta");
        composed!.Content.Should().Be(Golden(slug),
            $"composing '{slug}' must produce the same bytes it did before the delta format "
            + "gained a mandatory artefacts section — the section is the repository's, not the "
            + "principles file's");
    }

    [Fact]
    public async Task Compose_DeltaWithArtefacts_SectionIsNotInTheRenderedPrinciples()
    {
        var composed = (await SourceAsync()).Compose("csharp");

        composed!.Content.Should().NotContain("## Artefacts",
            "an artefact is a file for the repository; restating it in principles.md would be a "
            + "second place for the two to disagree");
        composed.Artefacts.Should().NotBeEmpty(
            "the ecosystem this estate runs declares its enforcement files");
        composed.Artefacts.Should().OnlyContain(a => a.Content.Length > 0,
            "an artefact with no content is a heading nobody finished");
    }

    [Fact]
    public async Task Compose_DeltaDeclaringNoArtefacts_YieldsNoneRatherThanFailing()
    {
        var composed = (await SourceAsync()).Compose("rust");

        composed!.Artefacts.Should().BeEmpty(
            "a section stated empty is an answer, and it must not be read as an entry");
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("rust")]
    [InlineData("typescript")]
    public async Task EmbeddedCatalog_EveryDelta_DeclaresItsArtefactsSection(string slug)
    {
        // 2026-09-13-fcc1 made the section mandatory in the catalog's own validator. This is the
        // assertion that fails HERE when a later pin ships a delta without it — the validator
        // runs in the other repository and neither gate sees the other.
        var delta = await DeltaAsync(slug);

        delta.Should().Contain("## Artefacts",
            $"the pinned '{slug}' delta must declare its enforcement files, or state that it "
            + "declares none — an omitted section reads the same as an unfinished one");
    }

    private async Task<string> DeltaAsync(string slug)
    {
        var root = await RootAsync();
        return await File.ReadAllTextAsync(
            Path.Combine(root, "principles", "deltas", $"{slug}.md"));
    }

    private static string Golden(string slug) => File.ReadAllText(
        Path.Combine(ArchitectureSources.TestSourceRoot, "Skills", "Goldens", $"principles-{slug}.md"));

    private async Task<CatalogPrinciplesTemplateSource> SourceAsync()
    {
        var path = new Mock<ISkillsCatalogPath>();
        path.Setup(p => p.Root).Returns(await RootAsync());
        return new CatalogPrinciplesTemplateSource(
            path.Object, NullLogger<CatalogPrinciplesTemplateSource>.Instance);
    }

    private async Task<string> RootAsync()
    {
        var handler = new EmbeddedSourceHandler(
            new EmbeddedSkillsCatalog(),
            new CatalogTarballExtractor(NullLogger<CatalogTarballExtractor>.Instance),
            new SkillsCacheMarker(NullLogger<SkillsCacheMarker>.Instance),
            NullLogger<EmbeddedSourceHandler>.Instance);
        var resolution = await handler.ResolveAsync(
            new SkillsConfig { Source = SkillsSourceMode.Embedded, CacheDir = _cacheDir },
            CancellationToken.None);
        return resolution.Root;
    }
}
