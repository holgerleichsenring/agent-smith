using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Tests.ConfigStudio;
using FluentAssertions;

namespace AgentSmith.Tests.DesignSources;

/// <summary>
/// 2026-10-01-7f7aa: the studio store holds design sources like any catalog kind — a project
/// may name only a source that exists, a source only a secret that exists, and neither the
/// source nor its secret can be deleted while something names it.
/// </summary>
public sealed class DesignSourceStoreTests : IDisposable
{
    private readonly DbConfigTestHarness _h = new();
    private static readonly ChangeAttribution Tester = new("tester");

    private const string Yaml = """
        agents:
          claude-default: { type: claude, model: sonnet-4 }
        repos:
          test-repo: { type: github, url: https://github.com/test/repo, auth: token }
        trackers:
          test-ado: { type: azure_devops, organization: testorg, project: TestProject, auth: token }
        secrets:
          figma_token: ${AGENTSMITH_TEST_FIGMA_TOKEN}
        design_sources:
          brand: { vendor: figma, auth: figma_token, display_name: Brand library }
        projects:
          testproject:
            agent: claude-default
            tracker: test-ado
            repos: [test-repo]
            pipeline: code
            design_sources: [brand]
        """;

    [Fact]
    public void UpsertProject_UnknownDesignSource_IsRefused()
    {
        _h.Import(Yaml);

        var act = () => _h.Store.UpsertProject(Project() with { DesignSources = ["ghost"] }, Tester);

        act.Should().Throw<ConfigurationException>().WithMessage("*unknown design source 'ghost'*");
    }

    [Fact]
    public void UpsertProject_WithoutDesignSources_KeepsStoredList()
    {
        _h.Import(Yaml);

        _h.Store.UpsertProject(Project() with { DesignSources = null }, Tester);

        _h.Store.GetProjects().Single().DesignSources.Should().Equal("brand");
    }

    [Fact]
    public void DeleteSecret_NamedByADesignSource_IsRefused()
    {
        _h.Import(Yaml);

        var act = () => _h.Store.DeleteSecret("figma_token", Tester);

        act.Should().Throw<ConfigurationException>().WithMessage("*design_source/brand*");
        _h.Store.GetSecrets().Should().Contain(s => s.Id == "figma_token");
    }

    [Fact]
    public void DeleteDesignSource_ReferencedByAProject_IsRefused()
    {
        _h.Import(Yaml);

        var act = () => _h.Store.DeleteDesignSource("brand", Tester);

        act.Should().Throw<ConfigurationException>().WithMessage("*project/testproject*");
    }

    [Fact]
    public void UpsertDesignSource_SavesTheSecretNameAndShowsInChanges()
    {
        _h.Import(Yaml);

        _h.Store.UpsertDesignSource(new DesignSourceEntity("product", "figma", "figma_token"), Tester);

        _h.Store.GetDesignSources().Should().Contain(new DesignSourceEntity("product", "figma", "figma_token"));
        _h.Store.GetChanges().Should().Contain(c =>
            c.EntityType == ConfigEntityType.DesignSource && c.EntityId == "product");
    }

    [Fact]
    public void UpsertDesignSource_UnknownSecretOrVendor_IsRefused()
    {
        _h.Import(Yaml);

        _h.Store.Invoking(s => s.UpsertDesignSource(new DesignSourceEntity("x", "figma", "ghost"), Tester))
            .Should().Throw<ConfigurationException>().WithMessage("*unknown secret 'ghost'*");
        _h.Store.Invoking(s => s.UpsertDesignSource(new DesignSourceEntity("x", "sketch", "figma_token"), Tester))
            .Should().Throw<ConfigurationException>().WithMessage("*vendor 'sketch'*");
    }

    [Fact]
    public void ConfigImport_RoundTripsDesignSources()
    {
        _h.Import(Yaml);

        var exported = _h.Store.ExportYaml();
        using var second = new DbConfigTestHarness();
        second.Import(exported);

        second.Store.GetDesignSources().Should().ContainSingle()
            .Which.Should().Be(new DesignSourceEntity("brand", "figma", "figma_token", "Brand library"));
        second.Store.GetProjects().Single().DesignSources.Should().Equal("brand");
    }

    private static ProjectEntity Project() =>
        new("testproject", "claude-default", "test-ado", ["test-repo"], "code", ["code"]);

    public void Dispose() => _h.Dispose();
}
