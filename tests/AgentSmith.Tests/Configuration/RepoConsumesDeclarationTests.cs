using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// 2026-08-30-c6ec: a repository entry may declare the served interface it CONSUMES, so a
/// run knows which checkouts hold first-party call sites. The declaration rides the repos
/// catalogue the run already checks out — a role on an existing list is a smaller thing
/// than a second declaration site — and a wildcard entry may not carry one.
/// </summary>
public sealed class RepoConsumesDeclarationTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(),
        $"agentsmith-consumes-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    private const string Catalogs = """
        agents:
          a: { type: Claude }
        trackers:
          t: { type: GitHub, auth: t }
        connections:
          acme:
            type: azure_devops
            organization: acme-cloud
            project: Platform
            auth: token
        """;

    [Fact]
    public void Consumes_AnExactRepoEntry_CarriesTheInterfaceItConsumes()
    {
        Write($$"""
            {{Catalogs}}
            projects:
              demo:
                agent: a
                tracker: t
                repos:
                  - acme/Service.Api
                  - { repo: acme/Storefront.Web, consumes: Orders }
            """);

        var repos = Load().Projects["demo"].Repos;

        repos.Single(r => r.Name == "Storefront.Web").Consumes.Should().Be("Orders");
        repos.Single(r => r.Name == "Service.Api").Consumes.Should().BeNull(
            "a repository that declares nothing consumes nothing this run can read");
    }

    [Fact]
    public void Consumes_AWildcardRepoEntry_IsRefused()
    {
        Write($$"""
            {{Catalogs}}
            projects:
              demo:
                agent: a
                tracker: t
                repos:
                  - { repo: acme/Storefront.*, consumes: Orders }
            """);

        var act = () => Load();

        act.Should().Throw<ConfigurationException>().WithMessage("*exact repo reference*",
            "a wildcard names a set nobody enumerated, and the declaration is a claim about "
            + "one checkout's call sites");
    }

    private void Write(string yaml) => File.WriteAllText(_tempFile, yaml);

    private AgentSmithConfig Load() =>
        new YamlConfigurationLoader(
            new RawConfigMaterializer(
                new ProjectConfigNormalizer(),
                new EffectiveTriggerBuilder(),
                new DeploymentDefaultsApplier(),
                new ConfigCatalogResolver(),
                new AgentSmithPaths()),
            new NoOpSystemEventPublisher()).LoadConfig(_tempFile);
}
