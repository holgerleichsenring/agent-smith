using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;
using AgentSmith.Application.Services.Events;

namespace AgentSmith.Tests.Configuration;

public sealed class SandboxAgentImageYamlTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(),
        $"agentsmith-agent-image-yaml-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    [Fact]
    public void LoadConfig_TopLevelSandboxBlock_PopulatesGlobalDefaults()
    {
        File.WriteAllText(_tempFile, """
            sandbox:
              agent_registry: corp-registry
              agent_version: 0.48.0
            projects: {}
            secrets: {}
            """);
        var loader = new YamlConfigurationLoader(new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(), new ConfigCatalogResolver(), new AgentSmithPaths(), new NoOpSystemEventPublisher());

        var cfg = loader.LoadConfig(_tempFile);

        cfg.Sandbox.AgentRegistry.Should().Be("corp-registry");
        cfg.Sandbox.AgentVersion.Should().Be("0.48.0");
    }

    [Fact]
    public void LoadConfig_PerProjectAgentOverride_PopulatesSandboxConfig()
    {
        File.WriteAllText(_tempFile, """
            sandbox:
              agent_registry: holgerleichsenring
              agent_version: 0.48.0
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
                  agent_registry: corp-mirror
                  agent_version: 0.49.0-beta
            secrets: {}
            """);
        var loader = new YamlConfigurationLoader(new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(), new ConfigCatalogResolver(), new AgentSmithPaths(), new NoOpSystemEventPublisher());

        var cfg = loader.LoadConfig(_tempFile);

        cfg.Projects["demo"].Sandbox!.AgentRegistry.Should().Be("corp-mirror");
        cfg.Projects["demo"].Sandbox!.AgentVersion.Should().Be("0.49.0-beta");
    }

    [Fact]
    public void LoadConfig_NoSandboxBlock_KeepsDefaultRegistryAndEmptyVersion()
    {
        File.WriteAllText(_tempFile, """
            projects: {}
            secrets: {}
            """);
        var loader = new YamlConfigurationLoader(new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(), new ConfigCatalogResolver(), new AgentSmithPaths(), new NoOpSystemEventPublisher());

        var cfg = loader.LoadConfig(_tempFile);

        cfg.Sandbox.AgentRegistry.Should().Be("holgerleichsenring");
        cfg.Sandbox.AgentVersion.Should().Be("");
    }
}
