using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Skills;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Skills;

/// <summary>
/// p0313b: references live at the catalog root next to principles/ and patterns/ —
/// shared content, not skills. This is the end-to-end pair: a catalog on disk plus
/// the resolver that reads it, so the two halves cannot agree on a layout the
/// packaged tarball does not have.
/// </summary>
public sealed class CatalogSkillReferenceSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    [Fact]
    public void TryRead_ReferenceShippedByTheCatalog_ReturnsItsBody()
    {
        WriteReference("spawn-budget", "The budget is run-wide and finite.");

        CreateSut().TryRead("spawn-budget").Should().Be("The budget is run-wide and finite.");
    }

    [Fact]
    public void TryRead_ReferenceTheCatalogDoesNotShip_ReturnsNull()
    {
        Directory.CreateDirectory(_root);

        CreateSut().TryRead("spawn-budget").Should().BeNull(
            "the source answers whether it has the file; refusing to render is the resolver's job");
    }

    [Fact]
    public void ResolveBody_AgainstARealCatalog_InlinesTheFileContent()
    {
        WriteReference("spawn-budget", "Name every task.\n");
        var body = new SkillBodyResolver(CreateSut()).ResolveBody(
            new RoleSkillDefinition { Name = "security-master", Rules = "## Parallelism\n{{ref:spawn-budget}}" },
            SkillRole.Master);

        body.Should().Be("## Parallelism\nName every task.");
    }

    private void WriteReference(string slug, string content)
    {
        var dir = Path.Combine(_root, "references");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{slug}.md"), content);
    }

    private CatalogSkillReferenceSource CreateSut()
    {
        var path = new Mock<ISkillsCatalogPath>();
        path.Setup(p => p.Root).Returns(_root);
        return new CatalogSkillReferenceSource(path.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
