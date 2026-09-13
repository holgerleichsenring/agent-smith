using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// 2026-09-13-5fa0: what the STORE must not do to a template declaration — wipe it on a
/// save that never knew the field, and let its target be deleted out from under it.
/// </summary>
public sealed class ProjectTemplateStoreTests : IDisposable
{
    private readonly DbConfigTestHarness _h = new();
    private static readonly ChangeAttribution Tester = new("tester");

    private const string SampleYaml = """
        agents:
          a: { type: claude, model: sonnet-4 }
        repos:
          target: { type: github, url: https://github.com/t/target, auth: token }
          reference: { type: github, url: https://github.com/t/reference, auth: token }
        trackers:
          t: { type: azure_devops, organization: o, project: P, auth: token }
        projects:
          app:
            agent: a
            tracker: t
            repos: [target]
            pipeline: code
            templates:
              - context: server
                project: refapp
                repo: reference
                template_context: server
                revision: v2.1.0
          refapp:
            agent: a
            tracker: t
            repos: [reference]
            pipeline: code
        """;

    [Fact]
    public void UpsertProject_EntityOmitsTemplates_StoredDeclarationSurvives()
    {
        _h.Import(SampleYaml);
        var stored = _h.Store.GetProjects().Single(p => p.Id == "app");
        stored.Templates.Should().ContainSingle();

        // What a client that predates the field sends: everything it knows, and no templates.
        _h.Store.UpsertProject(stored with { Templates = null }, Tester);

        _h.Store.GetProjects().Single(p => p.Id == "app").Templates
            .Should().ContainSingle("a save that cannot see the field must not erase it");
    }

    [Fact]
    public void UpsertProject_EntityCarriesAnEmptyList_ClearsTheDeclaration()
    {
        _h.Import(SampleYaml);
        var stored = _h.Store.GetProjects().Single(p => p.Id == "app");

        _h.Store.UpsertProject(stored with { Templates = [] }, Tester);

        _h.Store.GetProjects().Single(p => p.Id == "app").Templates
            .Should().BeEmpty("an empty list is an answer, unlike an absent one");
    }

    [Fact]
    public void DeleteProject_ReferencedAsATemplate_IsRefused()
    {
        _h.Import(SampleYaml);

        var act = () => _h.Store.DeleteProject("refapp", Tester);

        act.Should().Throw<ConfigurationException>().WithMessage("*referenced by*");
    }

    [Fact]
    public void ExportImport_ProjectWithTemplates_RoundTripsUnchanged()
    {
        _h.Import(SampleYaml);

        var exported = _h.Store.ExportYaml();
        _h.Import(exported, force: true);

        var template = _h.Store.GetProjects().Single(p => p.Id == "app").Templates
            .Should().ContainSingle().Subject;
        template.Should().Be(new TemplateReference("server", "refapp", "reference", "server", "v2.1.0"));
    }

    public void Dispose() => _h.Dispose();
}
