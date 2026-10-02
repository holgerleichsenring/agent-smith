using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Retired;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// 2026-10-02-5ab2f: pipeline_storage set the TTL of in-flight run artifacts in Redis, and
/// nothing stores them there any more. A file or a store that still sets it keeps loading and
/// is told, as an advisory, that the block is ignored.
/// </summary>
public sealed class RetiredPipelineStorageConfigTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"agentsmith-storage-{Guid.NewGuid():N}.yml");
    private readonly RetiredConfigKeyDetector _detector = new(new RawConfigTreeReader(), new ConfigKeyPathMatcher());

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    [Fact]
    public void ConfigFileWithPipelineStorage_Loads_WithARetiredKeyAdvisory()
    {
        const string yaml = """
            agents:
              claude: { type: anthropic, model: claude-sonnet-4-20250514 }
            pipeline_storage:
              redis_ttl_hours: 4
            projects: {}
            secrets: {}
            """;
        File.WriteAllText(_tempFile, yaml);

        var config = new YamlConfigurationLoader(Materializer(), new NoOpSystemEventPublisher()).LoadConfig(_tempFile);

        config.Agents.Should().ContainKey("claude");
        var finding = _detector.InYaml(yaml, _tempFile).Should().ContainSingle().Which;
        finding.Field.Should().Be("pipeline_storage");
        finding.Severity.Should().Be(StartupFindingSeverity.Advisory);
    }

    [Fact]
    public void StoredPipelineStorageDocument_Loads_WithARetiredKeyAdvisory()
    {
        var docs = new Mock<IConfigDocumentStore>();
        docs.Setup(d => d.LoadAll()).Returns(
        [
            new ConfigDocRow("pipeline_storage", "default", """{"RedisTtlHours":4}""", 1),
            new ConfigDocRow("agent", "claude", """{"Type":"anthropic","Model":"sonnet"}""", 1),
        ]);
        var findings = new StartupFindings();

        var config = new DbConfigurationLoader(docs.Object, new ConfigDocumentAssembler(), Materializer(),
            Bootstrap(), findings: findings, retiredKeys: _detector).LoadConfig("ignored");

        config.Agents.Should().ContainKey("claude", "a row of a type the taxonomy no longer knows is skipped");
        findings.All.Should().ContainSingle(f => f.Field == "pipeline_storage")
            .Which.Severity.Should().Be(StartupFindingSeverity.Advisory);
        findings.All.Should().NotContain(f => f.IsBlocking);
    }

    private static RawConfigMaterializer Materializer() => new(
        new ProjectConfigNormalizer(), new EffectiveTriggerBuilder(), new DeploymentDefaultsApplier(),
        new ConfigCatalogResolver(), new AgentSmithPaths());

    private BootstrapConfigReader Bootstrap()
    {
        File.WriteAllText(_tempFile, "persistence:\n  provider: sqlite\n  connection_string: 'Data Source=x.db'\n");
        return new BootstrapConfigReader(new FixedLocation(_tempFile), new RawConfigYaml(),
            new AuthEnvironmentOverlay(), new PersistenceEnvironmentOverlay());
    }

    private sealed record FixedLocation(string ConfigPath) : IConfigStoreLocation;
}
