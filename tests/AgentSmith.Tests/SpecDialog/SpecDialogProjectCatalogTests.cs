using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-15-6f8d: what a dialog may OPEN is one entry per address. A duplicated
/// declaration used to be offered twice — a template a turn cannot open a second time, on a
/// row that collides with itself on its key — while the config studio's form goes on showing
/// every declaration, because what can be deleted is a different question.
/// </summary>
public sealed class SpecDialogProjectCatalogTests
{
    [Fact]
    public void Catalog_TwoDeclarationsOfOneAddress_ProjectOneTemplateCarryingTheOwners()
    {
        var catalog = new SpecDialogProjectCatalog(Loader(
            Template("house", "template-repo", "v1.2"),
            Template("annex", "other-repo", "v9.9")));

        var all = catalog.All().Should().ContainSingle().Which;
        var open = catalog.Of("sample", ["repo-a"]);

        all.Templates.Should().ContainSingle().Which.Should().Be(
            new AgentSmith.Server.Models.SpecDialogTemplateView(
                "template:default", "template-repo", "v1.2"),
            "the owner's repository and revision are what the one opened scope is");
        open.Templates.Should().BeEquivalentTo(all.Templates,
            "both reads go through the one helper, so an open session sees what a new one does");
    }

    private static ProjectTemplate Template(string templateContext, string repo, string revision) =>
        new("default", templateContext, revision, new RepoConnection { Name = repo });

    private static IConfigurationLoader Loader(params ProjectTemplate[] templates)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["sample"] = new()
                {
                    Name = "sample",
                    Repos = [new RepoConnection { Name = "repo-a" }],
                    Templates = templates,
                },
            },
        });
        return loader.Object;
    }
}
