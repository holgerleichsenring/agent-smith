using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;
using AgentSmith.Application.Services.Events;

namespace AgentSmith.Tests.Configuration;

public sealed class SandboxResourcesYamlTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(),
        $"agentsmith-sandbox-yaml-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    [Fact]
    public void LoadConfig_SandboxResourcesPresent_PopulatesResourceLimitsRecord()
    {
        File.WriteAllText(_tempFile, """
            agents:
              a: { type: Claude }
            repos:
              r: { type: Local, path: ./repo, auth: none }
            trackers:
              t: { type: GitHub, auth: token }
            projects:
              demo:
                agent: a
                tracker: t
                repos: [r]
                sandbox:
                  resources:
                    cpu_request: 500m
                    cpu_limit: 2000m
                    memory_request: 1Gi
                    memory_limit: 4Gi
            secrets: {}
            """);
        var loader = new YamlConfigurationLoader(new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(), new ConfigCatalogResolver(), new AgentSmithPaths(), new NoOpSystemEventPublisher());

        var cfg = loader.LoadConfig(_tempFile);

        cfg.Projects["demo"].Sandbox.Should().NotBeNull();
        cfg.Projects["demo"].Sandbox!.Resources.Should().Be(
            new ResourceLimits("500m", "2000m", "1Gi", "4Gi"));
    }

    [Fact]
    public void LoadConfig_SandboxResourcesAbsent_LeavesResourcesNull()
    {
        File.WriteAllText(_tempFile, """
            agents:
              a: { type: Claude }
            repos:
              r: { type: Local, path: ./repo, auth: none }
            trackers:
              t: { type: GitHub, auth: token }
            projects:
              demo:
                agent: a
                tracker: t
                repos: [r]
                sandbox:
                  toolchain_image: my-registry/dotnet-sdk:8
            secrets: {}
            """);
        var loader = new YamlConfigurationLoader(new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(), new ConfigCatalogResolver(), new AgentSmithPaths(), new NoOpSystemEventPublisher());

        var cfg = loader.LoadConfig(_tempFile);

        cfg.Projects["demo"].Sandbox!.Resources.Should().BeNull();
        cfg.Projects["demo"].Sandbox!.ToolchainImage.Should().Be("my-registry/dotnet-sdk:8");
    }
}
