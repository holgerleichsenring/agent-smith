using AgentSmith.Application.Services.Activation;
using AgentSmith.Application.Services.Events;
using AgentSmith.Cli.Commands;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Skills;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Skills;

/// <summary>
/// p0514: the overlay is orthogonal to how the base arrived, so it goes through
/// the REAL resolver on top of the REAL embedded catalog and on top of a mounted
/// directory. The materialized root is an ordinary catalog directory, which is
/// why the existing <c>validate-concepts --skills-path</c> verb works on it and
/// no second validation verb was built.
/// </summary>
public sealed class SkillsOverlayResolutionTests : IDisposable
{
    private readonly string _temp = Path.Combine(
        Path.GetTempPath(), $"agentsmith-overlay-res-{Guid.NewGuid():N}");

    private string CacheDir => Path.Combine(_temp, "cache");
    private string OverlayDir => Path.Combine(_temp, "overlay");
    private string LayeredRoot => CacheDir + "-overlay";

    public void Dispose()
    {
        try { Directory.Delete(_temp, recursive: true); } catch (IOException) { }
        try { Directory.Delete(LayeredRoot, recursive: true); } catch (IOException) { }
    }

    private void GivenOverlaySkill()
    {
        var dir = Path.Combine(OverlayDir, "skills", "_masters", "house-master");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), "operator master");
    }

    private static SkillsCatalogResolver Resolver(params ISkillsSourceHandler[] handlers) => new(
        handlers,
        new SkillsOverlayMaterializer(NullLogger<SkillsOverlayMaterializer>.Instance),
        new SkillsCatalogPath(),
        NullLogger<SkillsCatalogResolver>.Instance);

    private static EmbeddedSourceHandler EmbeddedHandler() => new(
        new EmbeddedSkillsCatalog(),
        new CatalogTarballExtractor(NullLogger<CatalogTarballExtractor>.Instance),
        new SkillsCacheMarker(NullLogger<SkillsCacheMarker>.Instance),
        NullLogger<EmbeddedSourceHandler>.Instance);

    [Fact]
    public async Task Overlay_AppliedToAnEmbeddedBase_Works()
    {
        GivenOverlaySkill();
        var config = new SkillsConfig
        {
            Source = SkillsSourceMode.Embedded, CacheDir = CacheDir, Overlay = OverlayDir,
        };

        var resolution = await Resolver(EmbeddedHandler())
            .EnsureResolvedAsync(config, CancellationToken.None);

        resolution.Root.Should().Be(LayeredRoot);
        resolution.Version.Should().MatchRegex(@"^v\d+\.\d+\.\d+$", "the base version is not rewritten");
        Directory.GetDirectories(Path.Combine(resolution.Root, "skills", "_masters"))
            .Should().HaveCountGreaterThan(1, "the embedded masters survive the layering");
        File.Exists(Path.Combine(resolution.Root, "skills", "_masters", "house-master", "SKILL.md"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Overlay_AppliedToAMountedPathBase_Works()
    {
        GivenOverlaySkill();
        var mounted = Path.Combine(_temp, "mounted");
        Directory.CreateDirectory(Path.Combine(mounted, "skills", "_masters", "pinned-master"));
        File.WriteAllText(
            Path.Combine(mounted, "skills", "_masters", "pinned-master", "SKILL.md"), "pinned");
        var config = new SkillsConfig
        {
            Source = SkillsSourceMode.Path, Path = mounted, CacheDir = CacheDir, Overlay = OverlayDir,
        };

        var resolution = await Resolver(new PathSourceHandler(NullLogger<PathSourceHandler>.Instance))
            .EnsureResolvedAsync(config, CancellationToken.None);

        resolution.Root.Should().Be(LayeredRoot);
        resolution.Overlay.Should().NotBeNullOrEmpty();
        File.Exists(Path.Combine(resolution.Root, "skills", "_masters", "pinned-master", "SKILL.md"))
            .Should().BeTrue("a mounted base is layered under the overlay, not replaced by it");
        File.Exists(Path.Combine(resolution.Root, "skills", "_masters", "house-master", "SKILL.md"))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ValidateConcepts_AgainstAMaterialisedOverlayRoot_ResolvesTheOfficialVocabulary()
    {
        GivenOverlaySkill();
        var config = new SkillsConfig
        {
            Source = SkillsSourceMode.Embedded, CacheDir = CacheDir, Overlay = OverlayDir,
        };
        var resolution = await Resolver(EmbeddedHandler())
            .EnsureResolvedAsync(config, CancellationToken.None);
        var skillsPath = Path.Combine(resolution.Root, "skills");

        var loader = VocabularyLoader();
        var vocabulary = loader.Load(skillsPath);
        vocabulary.Concepts.Should().NotBeEmpty(
            "the official concept vocabulary is one of the files the overlay did not replace");
        var concept = vocabulary.Concepts.Values.First(c => c.Type == ConceptType.Bool).Name;

        var skills = new Mock<ISkillLoader>();
        skills.Setup(s => s.LoadRoleDefinitions(It.IsAny<string>()))
            .Returns([new RoleSkillDefinition { Name = "house-master", Role = "master", ActivatesWhen = concept }]);
        var command = new ValidateConceptsCommand(
            loader, skills.Object,
            new ActivationExpressionParser(new ActivationExpressionTokenizer()),
            new ConceptWriterRegistry([]));

        // The registry is empty here, so the vocabulary's declared writers report
        // unbacked — that is this fixture, not the overlay. What the overlay owes
        // is that the operator's own skill resolves against the OFFICIAL vocabulary
        // it did not ship; before layering that lookup found nothing at all.
        command.Validate(skillsPath).Errors.Should().NotContain(e => e.Subject == "house-master",
            "the layered root is an ordinary catalog directory the existing verb accepts");
    }

    private static ConceptVocabularyLoader VocabularyLoader() => new(
        new NoOpEventPublisher(),
        new AsyncLocalRunContextAccessor(),
        new NoOpSystemEventPublisher(),
        NullLogger<ConceptVocabularyLoader>.Instance);
}
