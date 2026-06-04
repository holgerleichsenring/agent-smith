using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
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
        IReadOnlyList<RoleSkillDefinition> roles, ConceptVocabulary vocabulary)
    {
        var resolver = new Mock<ISkillsCatalogResolver>();
        resolver
            .Setup(r => r.EnsureResolvedAsync(It.IsAny<SkillsConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogResolution("/catalog", "v1", default, "", true));

        var loader = new Mock<ISkillLoader>();
        loader.Setup(l => l.LoadRoleDefinitions(It.IsAny<string>())).Returns(roles);
        loader.Setup(l => l.LoadVocabulary(It.IsAny<string>())).Returns(vocabulary);

        return new CatalogContentsReader(resolver.Object, loader.Object, new AgentSmithConfig());
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
        var dir = Path.Combine(Path.GetTempPath(), "catalog-body-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        const string markdown = "# auth-reviewer\n\nFinds broken authorization.";
        await File.WriteAllTextAsync(Path.Combine(dir, "SKILL.md"), markdown);

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
}
