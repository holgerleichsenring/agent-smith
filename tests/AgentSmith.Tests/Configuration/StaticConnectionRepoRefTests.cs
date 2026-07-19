using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;
using Xunit;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// p0285: exact (wildcard-free) connection repo refs resolve statically from the connection
/// without a glob expander; glob refs still require discovery; a per-repo default_branch
/// override wins over the connection default.
/// </summary>
public sealed class StaticConnectionRepoRefTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(),
        $"agentsmith-static-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    private const string ConnectionsBlock = """
        connections:
          acme:
            type: azure_devops
            organization: acme-cloud
            project: Platform
            auth: token
            default_branch: develop
        """;

    [Fact]
    public void ResolveRepos_ExactConnectionRef_ResolvesWithoutGlobExpander()
    {
        Write($$"""
            agents:
              a: { type: Claude }
            trackers:
              t: { type: GitHub, auth: t }
            {{ConnectionsBlock}}
            projects:
              demo:
                agent: a
                tracker: t
                repos: [acme/Service.Api]
            """);

        // Load() builds a ConfigCatalogResolver with NO glob expander -> proves static resolution.
        var config = Load();

        var repo = config.Projects["demo"].Repos.Single();
        repo.Name.Should().Be("Service.Api");
        repo.Url.Should().Be("https://dev.azure.com/acme-cloud/Platform/_git/Service.Api");
        repo.DefaultBranch.Should().Be("develop");
    }

    [Fact]
    public void ResolveRepos_GlobConnectionRef_StillRequiresDiscovery()
    {
        Write($$"""
            agents:
              a: { type: Claude }
            trackers:
              t: { type: GitHub, auth: t }
            {{ConnectionsBlock}}
            projects:
              demo:
                agent: a
                tracker: t
                repos: [acme/Service.*]
            """);

        var act = () => Load();
        act.Should().Throw<ConfigurationException>().WithMessage("*require repo discovery*");
    }

    [Fact]
    public void ResolveRepos_ExactRefWithBranchOverride_UsesOverrideElseConnectionDefault()
    {
        Write($$"""
            agents:
              a: { type: Claude }
            trackers:
              t: { type: GitHub, auth: t }
            {{ConnectionsBlock}}
            projects:
              demo:
                agent: a
                tracker: t
                repos:
                  - acme/Service.Api
                  - { repo: acme/Docs, default_branch: main }
            """);

        var config = Load();

        var repos = config.Projects["demo"].Repos;
        repos.Single(r => r.Name == "Service.Api").DefaultBranch.Should().Be("develop");   // connection default
        repos.Single(r => r.Name == "Docs").DefaultBranch.Should().Be("main");             // per-repo override
    }

    [Fact]
    public void ResolveRepos_ExactRefUnknownConnection_ThrowsConfigurationException()
    {
        Write($$"""
            agents:
              a: { type: Claude }
            trackers:
              t: { type: GitHub, auth: t }
            {{ConnectionsBlock}}
            projects:
              demo:
                agent: a
                tracker: t
                repos: [ghost/Service.Api]
            """);

        var act = () => Load();
        act.Should().Throw<ConfigurationException>().WithMessage("*connection 'ghost'*");
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
