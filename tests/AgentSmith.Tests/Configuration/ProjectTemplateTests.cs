using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// 2026-09-13-5fa0: a project declares, per context, a template it is built after. The
/// rules run in the LOADER because that is the only path that can author one — the studio
/// has no picker and neither import path calls the referential validator.
/// </summary>
public sealed class ProjectTemplateTests
{
    private const string Yaml = """
        agents:
          a: { provider: anthropic, model: m }
        trackers:
          t: { type: jira, url: https://t.test, project: P }
        repos:
          target: { url: https://git.test/target }
          reference: { url: https://git.test/reference }
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
    public void LoadConfig_TemplateResolves_CarriesTheTargetRepoConnection()
    {
        var (config, findings) = Load(Yaml);

        findings.Should().BeEmpty();
        var template = config.Projects["app"].Templates.Should().ContainSingle().Subject;
        template.Context.Should().Be("server");
        template.TemplateContext.Should().Be("server");
        template.Revision.Should().Be("v2.1.0");
        template.Repo.Url.Should().Be("https://git.test/reference",
            "a consumer must not have to re-read the catalog mid-run");
    }

    [Fact]
    public void LoadConfig_TemplateNamesUnknownProject_ReportsFinding()
    {
        var (_, findings) = Load(Yaml.Replace("project: refapp", "project: nope"));

        findings.Should().ContainSingle(f => f.Reason.Contains("unknown project 'nope'"));
    }

    [Fact]
    public void LoadConfig_TemplateRepoRefNotInThatProject_ReportsFinding()
    {
        var (_, findings) = Load(Yaml.Replace("repo: reference", "repo: target"));

        findings.Should().ContainSingle(f => f.Reason.Contains("does not carry"));
    }

    [Fact]
    public void LoadConfig_TemplateRefIsGlob_ReportsFinding()
    {
        var (_, findings) = Load(Yaml.Replace("repo: reference", "repo: conn/*"));

        findings.Should().ContainSingle(f => f.Reason.Contains("glob repo ref"));
    }

    [Fact]
    public void LoadConfig_TemplateCycleAcrossProjects_ReportsFinding()
    {
        var (_, findings) = Load(Yaml.Replace(
            """
                repos: [reference]
                pipeline: code
            """.TrimEnd(),
            """
                repos: [reference]
                pipeline: code
                templates:
                  - context: server
                    project: app
                    repo: target
                    template_context: server
            """.TrimEnd()));

        findings.Should().Contain(f => f.Reason.Contains("template cycle"));
    }

    [Fact]
    public void LoadConfig_NoTemplates_ResolvesToAnEmptyList()
    {
        var (config, findings) = Load(Yaml[..Yaml.IndexOf("    templates:", StringComparison.Ordinal)]
            + "  refapp:\n    agent: a\n    tracker: t\n    repos: [reference]\n    pipeline: code\n");

        findings.Should().BeEmpty();
        config.Projects["app"].Templates.Should().BeEmpty();
    }

    [Fact]
    public void ValidateProject_TemplateNamesUnknownProject_Refused()
    {
        var catalog = Catalog(new TemplateReference("server", "nope", "reference", "server"));

        var act = () => ConfigReferentialValidator.ValidateProject(catalog.Projects[0], catalog);

        act.Should().Throw<ConfigurationException>().WithMessage("*unknown project 'nope'*");
    }

    [Fact]
    public void ValidateProject_TemplatesAbsent_IsNotJudged()
    {
        var catalog = Catalog(templates: null);

        var act = () => ConfigReferentialValidator.ValidateProject(catalog.Projects[0], catalog);

        act.Should().NotThrow("absent means the caller has nothing to say, not that there are none");
    }

    private static ConfigCatalog Catalog(params TemplateReference[]? templates) =>
        Catalog(templates?.ToList());

    private static ConfigCatalog Catalog(IReadOnlyList<TemplateReference>? templates) => new(
        Agents: [new AgentEntity { Id = "a" }],
        Trackers: [new TrackerEntity { Id = "t" }],
        Repos: [new RepoEntity("target", "https://git.test/target", null),
                new RepoEntity("reference", "https://git.test/reference", null)],
        Projects: [new ProjectEntity(
            "app", "a", "t", ["target"], "code", ["code"], null, null, templates)],
        McpServers: [], Secrets: [], Connections: []);

    private static (AgentSmithConfig Config, IReadOnlyList<StartupFinding> Findings) Load(string yaml)
    {
        var raw = new RawConfigYaml().Deserialize(yaml);
        var resolver = new ConfigCatalogResolver();
        var config = resolver.Resolve(raw);
        return (config, resolver.LastFindings);
    }
}
