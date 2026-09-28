using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Retired;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// No run spawns an orchestrator container, so the image pin and the per-project pod sizing
/// retire. The wall-time ceiling in the same block and deployment.version stay read.
/// </summary>
public sealed class RetiredOrchestratorConfigTests
{
    private readonly RetiredConfigKeyDetector _detector = new(new RawConfigTreeReader(), new ConfigKeyPathMatcher());

    [Fact]
    public void RetiredConfigKeys_OrchestratorBlock_ReportsAdvisory()
    {
        const string yaml = """
            deployment:
              version: 0.49.0
            orchestrator:
              registry: ghcr.io/sample
              version: 0.49.0
              max_run_wall_time_seconds: 3600
            projects:
              todolist:
                orchestrator:
                  version: 0.48.0
                  resources:
                    memory_limit: 2Gi
            """;

        var findings = _detector.InYaml(yaml, "config/agentsmith.yml");

        findings.Select(f => f.Field).Should().BeEquivalentTo(
            "orchestrator.registry", "orchestrator.version",
            "projects.todolist.orchestrator.version", "projects.todolist.orchestrator.resources");
        findings.Should().OnlyContain(f => f.Severity == StartupFindingSeverity.Advisory);
    }

    [Fact]
    public void RetiredConfigKeys_WallTimeAndDeploymentVersion_AreStillRead()
    {
        const string yaml = """
            deployment:
              registry: ghcr.io/sample
              version: 0.49.0
            orchestrator:
              max_run_wall_time_seconds: 3600
            """;

        _detector.InYaml(yaml, "config/agentsmith.yml").Should().BeEmpty();
    }

    [Fact]
    public void RetiredConfigKeys_StoredOrchestratorWithDefaults_IsSilent()
    {
        ConfigDocRow[] rows =
        [
            new("orchestrator", "default", """{"Registry":"","Version":"","MaxRunWallTimeSeconds":1800}""", 1),
            new("project", "todolist", """{"Name":"todolist","Orchestrator":null}""", 1),
        ];

        _detector.InStoredDocuments(rows).Should().BeEmpty(
            "every stored configuration carries the orchestrator row with its empty defaults");
    }

    [Fact]
    public void RetiredConfigKeys_StoredOrchestratorPin_ReportsAdvisory()
    {
        ConfigDocRow[] rows =
        [
            new("orchestrator", "default", """{"Registry":"","Version":"0.49.0","MaxRunWallTimeSeconds":1800}""", 1),
        ];

        _detector.InStoredDocuments(rows).Should().ContainSingle()
            .Which.Field.Should().Be("orchestrator.version");
    }
}
