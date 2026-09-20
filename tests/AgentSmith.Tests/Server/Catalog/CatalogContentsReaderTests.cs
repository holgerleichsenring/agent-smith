using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Skills;
using AgentSmith.Server.Services.Catalog;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Server.Catalog;

public sealed class CatalogContentsReaderTests
{
    private static RoleSkillDefinition Role(string name, string? role, string description, string? dir = null) =>
        new() { Name = name, Role = role, Description = description, SkillDirectory = dir };

    private static ConceptVocabulary Vocabulary() => new(new Dictionary<string, ProjectConcept>
    {
        ["authentication"] = new(
            "authentication", "Whether the target API authenticates requests",
            ConceptType.Bool, null, null, []),
    });

    private static CatalogContentsReader Reader(
        IReadOnlyList<RoleSkillDefinition> roles,
        ConceptVocabulary vocabulary,
        CatalogResolution? resolution = null,
        SkillsConfig? skills = null,
        TimeProvider? clock = null)
    {
        // 2026-09-18-84be: the REAL SkillsCatalogPath, because the phrase under test is the
        // one a resolution mints for itself — a stubbed Origin would prove only that the
        // reader copies a string a test wrote.
        var catalogPath = new SkillsCatalogPath();
        var resolved = resolution ?? new CatalogResolution("/catalog", "v1", default, "", true);
        var resolver = new Mock<ISkillsCatalogResolver>();
        resolver
            .Setup(r => r.EnsureResolvedAsync(It.IsAny<SkillsConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                catalogPath.Set(resolved);
                return resolved;
            });

        return new CatalogContentsReader(
            resolver.Object,
            Loader(roles, vocabulary).Object,
            catalogPath,
            clock ?? TimeProvider.System,
            new AgentSmithConfig { Skills = skills ?? new SkillsConfig() });
    }

    private static Mock<ISkillLoader> Loader(
        IReadOnlyList<RoleSkillDefinition> roles, ConceptVocabulary vocabulary)
    {
        var loader = new Mock<ISkillLoader>();
        loader.Setup(l => l.LoadRoleDefinitions(It.IsAny<string>())).Returns(roles);
        loader.Setup(l => l.LoadVocabulary(It.IsAny<string>())).Returns(vocabulary);
        return loader;
    }

    private static string WriteSkill(string name, string markdown)
    {
        var dir = Path.Combine(Path.GetTempPath(), "catalog-body-" + Guid.NewGuid().ToString("N"), name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), markdown);
        return dir;
    }

    [Fact]
    public async Task CatalogContentsEndpoint_ReturnsMastersWithDescriptions()
    {
        var reader = Reader(
            new[]
            {
                Role("coding-agent-master", "master", "Drives agentic code edits"),
                Role("auth-reviewer", "investigator", "Finds broken authz"),
            },
            Vocabulary());

        var contents = await reader.GetContentsAsync(CancellationToken.None);

        contents.Ready.Should().BeTrue();
        contents.Masters.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                new CatalogEntry("coding-agent-master", "master", "Drives agentic code edits"));
        contents.Skills.Should().ContainSingle().Which.Name.Should().Be("auth-reviewer");
    }

    [Fact]
    public async Task CatalogContentsEndpoint_ReturnsSkillBody_AsMarkdown()
    {
        const string markdown = "# auth-reviewer\n\nFinds broken authorization.";
        var dir = WriteSkill("auth-reviewer", markdown);

        var reader = Reader(new[] { Role("auth-reviewer", "investigator", "desc", dir) }, Vocabulary());

        var body = await reader.GetSkillBodyAsync("auth-reviewer", CancellationToken.None);

        body.Should().NotBeNull();
        body!.Name.Should().Be("auth-reviewer");
        body.Markdown.Should().Be(markdown);

        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public async Task CatalogContentsEndpoint_ReturnsConcepts_WithTypeAndDescription()
    {
        var reader = Reader(Array.Empty<RoleSkillDefinition>(), Vocabulary());

        var contents = await reader.GetContentsAsync(CancellationToken.None);

        contents.Concepts.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                new CatalogConcept("authentication", "Bool", "Whether the target API authenticates requests"));
    }

    // 2026-09-18-84be: the page renders skill text; without this the response said nothing
    // about which of the four source modes produced it.
    [Fact]
    public async Task Catalog_Response_CarriesTheResolvedOrigin()
    {
        var reader = Reader(
            new[] { Role("auth-reviewer", "investigator", "Finds broken authz") },
            Vocabulary(),
            new CatalogResolution("/mnt/skills", "v5.2.0", SkillsSourceMode.Path, "", true));

        var contents = await reader.GetContentsAsync(CancellationToken.None);

        contents.Origin!.Phrase.Should().Be("path v5.2.0 at /mnt/skills");
    }

    // An overlaid install renders the OPERATOR's file, so an origin that named the base
    // alone would be a half-truth about the text on the page. The phrase carries a bare
    // fingerprint and the synthesised union root — neither of which the operator typed —
    // so the CONFIGURED overlay directory rides beside it.
    [Fact]
    public async Task Catalog_OverlaidInstallation_TheOriginNamesTheOverlay()
    {
        var reader = Reader(
            Array.Empty<RoleSkillDefinition>(),
            Vocabulary(),
            new CatalogResolution(
                "/var/cache/skills-overlay", "v5.2.0", SkillsSourceMode.Embedded, "", true, "4f2a9c10b7e3"),
            new SkillsConfig { Overlay = "/etc/agentsmith/skills-overlay" });

        var contents = await reader.GetContentsAsync(CancellationToken.None);

        contents.Origin!.Phrase.Should().Be(
            "embedded v5.2.0 + overlay 4f2a9c10b7e3 at /var/cache/skills-overlay");
        contents.Origin.OverlayPath.Should().Be("/etc/agentsmith/skills-overlay");
    }

    // The default carries no pin at all — the embedded catalog is what an untouched
    // installation runs, and the phrase has to be able to say so. No overlay is layered,
    // so no overlay path is claimed.
    [Fact]
    public async Task Catalog_EmbeddedDefault_TheOriginNamesTheEmbeddedSource()
    {
        var reader = Reader(
            Array.Empty<RoleSkillDefinition>(),
            Vocabulary(),
            new CatalogResolution(
                "/var/cache/skills", "v5.2.0", SkillsSourceMode.Embedded, "", true),
            new SkillsConfig { Overlay = "/etc/agentsmith/skills-overlay" });

        var contents = await reader.GetContentsAsync(CancellationToken.None);

        contents.Origin!.Phrase.Should().Be("embedded v5.2.0 at /var/cache/skills");
        contents.Origin.OverlayPath.Should().BeNull();
    }

    // The contents are cached, so a version printed beside them says nothing about their
    // age. The reading time is stamped at the same gate that loaded them.
    [Fact]
    public async Task Catalog_Response_SaysWhenTheContentsWereRead()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 9, 18, 14, 30, 0, TimeSpan.Zero));
        var reader = Reader(Array.Empty<RoleSkillDefinition>(), Vocabulary(), clock: clock);

        var contents = await reader.GetContentsAsync(CancellationToken.None);

        contents.Origin!.ReadAt.Should().Be(new DateTimeOffset(2026, 9, 18, 14, 30, 0, TimeSpan.Zero));
    }

    // The extractor and the overlay materializer rebuild into the SAME absolute root, and
    // any run re-resolves — so a cache keyed on nothing would serve the new text under the
    // old phrase. Listing, origin and body move together or not at all.
    [Fact]
    public async Task Catalog_BindingMovedUnderTheReader_ContentsAndBodyFollowTheNewBinding()
    {
        var first = WriteSkill("auth-reviewer", "# first edition");
        var second = WriteSkill("auth-reviewer", "# second edition");
        var catalogPath = new SkillsCatalogPath();
        var resolutions = new Queue<CatalogResolution>(
        [
            new CatalogResolution("/catalog", "v5.2.0", SkillsSourceMode.Embedded, "", true),
            new CatalogResolution("/catalog", "v5.3.0", SkillsSourceMode.Embedded, "", true),
        ]);
        var resolver = new Mock<ISkillsCatalogResolver>();
        resolver
            .Setup(r => r.EnsureResolvedAsync(It.IsAny<SkillsConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var next = resolutions.Dequeue();
                catalogPath.Set(next);
                return next;
            });
        var directories = new Queue<string>([first, second]);
        var loader = new Mock<ISkillLoader>();
        loader.Setup(l => l.LoadRoleDefinitions(It.IsAny<string>()))
            .Returns(() => new[] { Role("auth-reviewer", "investigator", "desc", directories.Dequeue()) });
        loader.Setup(l => l.LoadVocabulary(It.IsAny<string>())).Returns(Vocabulary());
        var reader = new CatalogContentsReader(
            resolver.Object, loader.Object, catalogPath, TimeProvider.System, new AgentSmithConfig());

        var before = await reader.GetContentsAsync(CancellationToken.None);
        var beforeBody = await reader.GetSkillBodyAsync("auth-reviewer", CancellationToken.None);
        // What a run does: re-resolve, which re-points the process-wide catalog.
        catalogPath.Set(new CatalogResolution("/catalog", "v5.3.0", SkillsSourceMode.Embedded, "", true));
        var after = await reader.GetContentsAsync(CancellationToken.None);
        var afterBody = await reader.GetSkillBodyAsync("auth-reviewer", CancellationToken.None);

        before.Origin!.Phrase.Should().Be("embedded v5.2.0 at /catalog");
        beforeBody!.Markdown.Should().Be("# first edition");
        after.Origin!.Phrase.Should().Be("embedded v5.3.0 at /catalog");
        afterBody!.Markdown.Should().Be("# second edition");

        Directory.Delete(Path.GetDirectoryName(first)!, recursive: true);
        Directory.Delete(Path.GetDirectoryName(second)!, recursive: true);
    }

    // The swap can land DURING a body read: the file name is stable, so the bytes that come
    // back may already be the next binding's. Text the page's phrase does not describe is
    // not served — the next call re-loads and serves it under its own phrase.
    [Fact]
    public async Task Catalog_BindingMovesWhileABodyIsRead_NoBodyIsServed()
    {
        var dir = WriteSkill("auth-reviewer", "# whichever edition this is");
        // The process-wide path already names the NEXT binding by the time the read
        // completes — another resolve landed while this one was serving.
        var moved = new MovedOnCatalogPath("embedded v5.3.0 at /catalog");
        var resolver = new Mock<ISkillsCatalogResolver>();
        resolver
            .Setup(r => r.EnsureResolvedAsync(It.IsAny<SkillsConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogResolution("/catalog", "v5.2.0", SkillsSourceMode.Embedded, "", true));
        var reader = new CatalogContentsReader(
            resolver.Object,
            Loader(new[] { Role("auth-reviewer", "investigator", "desc", dir) }, Vocabulary()).Object,
            moved,
            TimeProvider.System,
            new AgentSmithConfig());

        var body = await reader.GetSkillBodyAsync("auth-reviewer", CancellationToken.None);

        body.Should().BeNull();

        Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// A catalog path the run re-pointed while this reader was serving — it names a
    /// binding the reader's own resolution did not produce.
    /// </summary>
    private sealed class MovedOnCatalogPath(string origin) : ISkillsCatalogPath
    {
        public string Root => "/catalog";

        public string Origin => origin;
    }
}
