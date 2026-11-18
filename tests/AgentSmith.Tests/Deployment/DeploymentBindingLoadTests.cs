using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;

namespace AgentSmith.Tests.Deployment;

/// <summary>2026-11-18-3863a: a deployment binding loads from YAML with its environments and survives the studio.</summary>
public sealed class DeploymentBindingLoadTests
{
    private const string Yaml = """
        agents:
          a: { provider: anthropic, model: m }
        trackers:
          t: { type: jira, url: https://t.test, project: P, auth: s, email: a@t.test }
        secrets: { s: x }
        repos:
          app-repo: { url: https://git.test/app, auth: s }
          delivery: { url: https://git.test/delivery, auth: s }
          gitops: { url: https://git.test/gitops, auth: s }
        projects:
          app:
            agent: a
            tracker: t
            repos: [app-repo]
            pipeline: code
            templates:
              - context: api
                kind: deployment
                project: platform
                repo: delivery
                template_context: k8s-gitops
                revision: v1.0.0
                environments:
                  - name: test
                    approval: false
                    parameters: { image: registry.example.com/app, replicas: "1" }
                  - name: prod
                    repo: gitops
          platform:
            agent: a
            tracker: t
            repos: [delivery]
            pipeline: code
        """;

    [Fact]
    public void LoadConfig_DeploymentBinding_CarriesKindAndEnvironments()
    {
        var (config, findings) = Load(Yaml);

        findings.Should().BeEmpty();
        var binding = config.Projects["app"].Templates.Should().ContainSingle().Subject;
        binding.IsDeployment.Should().BeTrue();
        var test = binding.Environments!.Single(e => e.Name == "test");
        test.Approval.Should().BeFalse();
        test.Repo.Should().BeNull("no repo means the context's own repository");
        test.Parameters.Should().Contain("replicas", "1");
        var prod = binding.Environments!.Single(e => e.Name == "prod");
        prod.Approval.Should().BeTrue("a person approves unless an operator turned it off");
        prod.Repo!.Url.Should().Be("https://git.test/gitops");
        config.Projects["app"].Repos.Select(r => r.Name).Should().Equal(["app-repo"], "a manifest repo is not checked out");
    }

    [Fact]
    public void LoadConfig_KindAbsent_IsCode()
    {
        var (config, _) = Load(Yaml.Replace("        kind: deployment\n", "").Replace(
            Yaml[Yaml.IndexOf("        environments:", StringComparison.Ordinal)..Yaml.IndexOf("  platform:", StringComparison.Ordinal)], ""));

        config.Projects["app"].Templates.Single().Kind.Should().Be(TemplateKinds.Code);
    }

    [Fact]
    public void LoadConfig_EnvironmentRepoDoesNotResolve_ReportsFinding()
    {
        var (_, findings) = Load(Yaml.Replace("repo: gitops", "repo: missing"));

        findings.Should().Contain(f => f.Reason.Contains("environment 'prod' names repo ref 'missing'"));
    }

    [Fact]
    public void LoadConfig_EnvironmentsOnACodeBinding_ReportsFinding()
    {
        var (_, findings) = Load(Yaml.Replace("kind: deployment", "kind: code"));

        findings.Should().Contain(f => f.Reason.Contains("is not of kind 'deployment'"));
    }

    [Fact]
    public void Studio_DeploymentBinding_RoundTripsThroughThePatch()
    {
        var reference = new TemplateReference("api", "platform", "delivery", "k8s-gitops", "v1", null,
            TemplateKinds.Deployment,
            [new DeploymentEnvironmentReference("test", null, false, new Dictionary<string, string> { ["replicas"] = "1" }),
             new DeploymentEnvironmentReference("prod", "gitops")]);
        var raw = new RawProjectEntry { Templates = TemplateReferenceMapping.ToRaw([reference]) };

        var back = TemplateReferenceMapping.ToTemplates(raw).Single();

        back.Kind.Should().Be(TemplateKinds.Deployment);
        back.Environments!.Select(e => e.Name).Should().Equal("test", "prod");
        back.Environments![0].Approval.Should().BeFalse();
        back.Environments![0].Parameters.Should().Contain("replicas", "1");
        back.Environments![1].Repo.Should().Be("gitops");
    }

    private static (AgentSmithConfig Config, IReadOnlyList<StartupFinding> Findings) Load(string yaml)
    {
        var resolver = new ConfigCatalogResolver();
        var config = resolver.Resolve(new RawConfigYaml().Deserialize(yaml));
        return (config, resolver.LastFindings);
    }
}
