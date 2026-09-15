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
/// p0313b: the catalog packages references/ alongside skills/, and a master that
/// cites {{ref:&lt;slug&gt;}} does not render without it. Those are two repositories
/// moving independently — the citation is authored in agent-smith-skills, the
/// packaging list is a shell script there, and the pin is here.
///
/// This runs the REAL embedded tarball through the REAL resolver: every master the
/// pinned catalog ships must resolve. A pin whose masters cite references the
/// tarball does not carry fails here, at build time, instead of at the first run
/// that loads that master.
/// </summary>
public sealed class EmbeddedCatalogReferencesTests : IDisposable
{
    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(),
        $"agentsmith-refs-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, recursive: true);
    }

    [Fact]
    public async Task EmbeddedCatalog_EveryMaster_ResolvesItsCitedReferences()
    {
        var root = await MaterializeAsync();
        var catalogPath = new Mock<ISkillsCatalogPath>();
        catalogPath.Setup(p => p.Root).Returns(root);
        var resolver = new SkillBodyResolver(new CatalogSkillReferenceSource(catalogPath.Object));

        var masters = Directory.GetFiles(
            Path.Combine(root, "skills", "_masters"), "SKILL.md", SearchOption.AllDirectories);
        masters.Should().NotBeEmpty("the pinned catalog must ship masters at all");

        foreach (var master in masters)
        {
            var name = Path.GetFileName(Path.GetDirectoryName(master))!;
            var resolve = () => resolver.ResolveBody(
                new RoleSkillDefinition { Name = name, Rules = File.ReadAllText(master) },
                SkillRole.Master);

            resolve.Should().NotThrow(
                $"master '{name}' must render against the catalog that ships it");
        }
    }

    /// <summary>
    /// 2026-09-13-6f35: the coding master must say where a TEMPLATE sits among its sources —
    /// principles first, the template for what is NEW, existing code for an EXTENSION. That
    /// wording is the catalog's own shared <c>source-precedence</c> section (2026-09-13-ab17)
    /// and 2026-09-13-4072 moved the pin to the release that carries it. The assertion was gated
    /// on the citation while the two phases were apart; the pin carries it now, so dropping
    /// the citation from a later release fails here instead of at the first run.
    /// </summary>
    [Fact]
    public async Task EmbeddedCatalog_CodingMaster_SaysWhereATemplateSitsAmongItsSources()
    {
        var root = await MaterializeAsync();
        var master = Directory
            .GetFiles(Path.Combine(root, "skills", "_masters"), "SKILL.md", SearchOption.AllDirectories)
            .SingleOrDefault(f => Path.GetFileName(Path.GetDirectoryName(f)) == "coding-agent-master");
        master.Should().NotBeNull("the coding master is the skill that types the code");

        var rules = File.ReadAllText(master!);
        rules.Should().Contain("{{ref:source-precedence}}",
            "the pinned master must CITE the shared order rather than restate one of its own");

        var catalogPath = new Mock<ISkillsCatalogPath>();
        catalogPath.Setup(p => p.Root).Returns(root);
        var body = new SkillBodyResolver(new CatalogSkillReferenceSource(catalogPath.Object))
            .ResolveBody(
                new RoleSkillDefinition { Name = "coding-agent-master", Rules = rules },
                SkillRole.Master);

        body.Should().Contain("template", "a cited order that never names the template orders nothing");
    }

    /// <summary>
    /// 2026-09-13-ed5a: the design partner cuts the epic, and a cut made without the house
    /// shape produces slices that cross the layers the template keeps apart. It must therefore
    /// state where a template sits among its sources — the catalog's own shared
    /// <c>source-precedence</c> section (2026-09-13-ab17), whose pin was moved by
    /// 2026-09-13-4072. Ungated with its two siblings, in the commit that moved the pin.
    /// </summary>
    [Fact]
    public async Task EmbeddedCatalog_DesignPartnerMaster_SaysWhereATemplateSitsAmongItsSources()
    {
        var root = await MaterializeAsync();
        var master = Directory
            .GetFiles(Path.Combine(root, "skills", "_masters"), "SKILL.md", SearchOption.AllDirectories)
            .SingleOrDefault(f => Path.GetFileName(Path.GetDirectoryName(f)) == "design-partner-master");
        master.Should().NotBeNull("the design partner is the skill that cuts the epic");

        var rules = File.ReadAllText(master!);
        rules.Should().Contain("{{ref:source-precedence}}",
            "the pinned master must CITE the shared order rather than restate one of its own");

        var catalogPath = new Mock<ISkillsCatalogPath>();
        catalogPath.Setup(p => p.Root).Returns(root);
        var body = new SkillBodyResolver(new CatalogSkillReferenceSource(catalogPath.Object))
            .ResolveBody(
                new RoleSkillDefinition { Name = "design-partner-master", Rules = rules },
                SkillRole.Master);

        body.Should().Contain("template", "a cited order that never names the template orders nothing");
    }

    /// <summary>
    /// 2026-09-13-7d9f: the derivation reads the EPIC a ticket is one slice of, and the section
    /// that carries it tells the model to follow the source order its instructions state rather
    /// than restating that order — so the master must state one, or the section points at
    /// nothing. Same shared <c>source-precedence</c> section (2026-09-13-ab17), same pin move
    /// (2026-09-13-4072), ungated the same way its two siblings are.
    /// </summary>
    [Fact]
    public async Task EmbeddedCatalog_SpecDerivationMaster_SaysWhereATemplateSitsAmongItsSources()
    {
        var root = await MaterializeAsync();
        var master = Directory
            .GetFiles(Path.Combine(root, "skills", "_masters"), "SKILL.md", SearchOption.AllDirectories)
            .SingleOrDefault(f => Path.GetFileName(Path.GetDirectoryName(f)) == "spec-derivation-master");
        master.Should().NotBeNull("the derivation is the skill that cuts one ticket into phases");

        var rules = File.ReadAllText(master!);
        rules.Should().Contain("{{ref:source-precedence}}",
            "the pinned master must CITE the shared order rather than restate one of its own");

        var catalogPath = new Mock<ISkillsCatalogPath>();
        catalogPath.Setup(p => p.Root).Returns(root);
        var body = new SkillBodyResolver(new CatalogSkillReferenceSource(catalogPath.Object))
            .ResolveBody(
                new RoleSkillDefinition { Name = "spec-derivation-master", Rules = rules },
                SkillRole.Master);

        body.Should().Contain("template", "a cited order that never names the template orders nothing");
    }

    private async Task<string> MaterializeAsync()
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
