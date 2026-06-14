using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Config;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0266: the config-snapshot mapper is the redaction boundary for the
/// dashboard's /api/config surface. These tests pin that secrets never reach
/// the wire (the whole point of the allow-list DTO) and that the reachability
/// edges + redacted fields are correct.
/// </summary>
public sealed class ConfigSnapshotMapperTests
{
    private const string SecretApiKey = "sk-super-secret-key-123";
    private const string SecretRepoAuth = "repo-pat-abc";
    private const string SecretTrackerAuth = "tracker-pat-xyz";
    private const string SecretConnString = "Host=db;Username=admin;Password=hunter2";

    private static AgentSmithConfig BuildConfig()
    {
        var agent = new AgentConfig
        {
            Type = "anthropic", Model = "claude-opus-4-1",
            ApiKeySecret = SecretApiKey,
            NetworkTimeoutSeconds = 300, MaxFixIterations = 3,
            RateLimit = new RateLimitConfig { RequestsPerMinute = 50, InputTokensPerMinute = 80_000 },
        };
        var repo = new RepoConnection
        {
            Name = "sample-server", Type = RepoType.GitHub,
            Url = "https://github.com/acme/sample-server.git",
            Auth = SecretRepoAuth, DefaultBranch = "main",
        };
        var tracker = new TrackerConnection
        {
            Name = "acme-jira", Type = TrackerType.Jira, Project = "OPS",
            Auth = SecretTrackerAuth, OpenStates = ["To Do", "In Progress"], DoneStatus = "Done",
        };
        return new AgentSmithConfig
        {
            Agents = new() { ["claude"] = agent },
            Repos = new() { ["sample-server"] = repo },
            Trackers = new() { ["acme-jira"] = tracker },
            Secrets = new Dictionary<string, string> { ["ANTHROPIC_KEY"] = SecretApiKey },
            Persistence = new PersistenceConfig { Provider = "postgresql", ConnectionString = SecretConnString },
            Projects = new()
            {
                ["ops"] = new ResolvedProject
                {
                    Name = "ops", Pipeline = "fix-bug", Agent = agent, Tracker = tracker,
                    Repos = [repo],
                    Pipelines = [new PipelineDefinition { Name = "fix-bug" }, new PipelineDefinition { Name = "security-scan" }],
                },
            },
        };
    }

    [Fact]
    public void ToSnapshot_WithSecrets_OmitsApiKeysTokensAndConnectionStrings()
    {
        var snapshot = ConfigSnapshotMapper.ToSnapshot(BuildConfig());

        var json = JsonSerializer.Serialize(snapshot);
        json.Should().NotContain(SecretApiKey);
        json.Should().NotContain(SecretRepoAuth);
        json.Should().NotContain(SecretTrackerAuth);
        json.Should().NotContain(SecretConnString);
        json.Should().NotContain("hunter2");
        // The persistence KIND is still surfaced — only the credentials are gone.
        snapshot.Globals.PersistenceProvider.Should().Be("postgresql");
    }

    [Fact]
    public void ToSnapshot_Project_EmitsEdgesToRepoTrackerAgentPipeline()
    {
        var snapshot = ConfigSnapshotMapper.ToSnapshot(BuildConfig());

        snapshot.Edges.Should().ContainEquivalentOf(new ConfigEdge("ops", "claude", "agent"));
        snapshot.Edges.Should().ContainEquivalentOf(new ConfigEdge("ops", "acme-jira", "tracker"));
        snapshot.Edges.Should().ContainEquivalentOf(new ConfigEdge("ops", "sample-server", "repo"));
        snapshot.Edges.Should().ContainEquivalentOf(new ConfigEdge("ops", "fix-bug", "pipeline"));
        snapshot.Edges.Should().ContainEquivalentOf(new ConfigEdge("ops", "security-scan", "pipeline"));
    }

    [Fact]
    public void ToSnapshot_Repo_ReducesUrlToHostOnly()
    {
        var snapshot = ConfigSnapshotMapper.ToSnapshot(BuildConfig());

        var repo = snapshot.Repos.Should().ContainSingle().Subject;
        repo.Host.Should().Be("github.com");
        repo.DefaultBranch.Should().Be("main");
    }

    [Fact]
    public void ToSnapshot_Globals_MapsSandboxOrchestratorLimitCostCap()
    {
        var config = BuildConfig();
        config.Sandbox.AgentVersion = "0.48.0";
        config.Sandbox.StepTimeoutSeconds = 900;

        var globals = ConfigSnapshotMapper.ToSnapshot(config).Globals;

        globals.Sandbox.AgentVersion.Should().Be("0.48.0");
        globals.Sandbox.StepTimeoutSeconds.Should().Be(900);
        globals.Orchestrator.MaxRunWallTimeSeconds.Should().Be(1800);
        globals.CostCap.Usd.Should().Be(5.0m);
        globals.Limits.MaxToolCallsPerSkill.Should().Be(30);
    }

    [Fact]
    public void ToSnapshot_Agent_KeepsTuningFieldsByCatalogName()
    {
        var agent = ConfigSnapshotMapper.ToSnapshot(BuildConfig()).Agents.Should().ContainSingle().Subject;

        agent.Name.Should().Be("claude");
        agent.Model.Should().Be("claude-opus-4-1");
        agent.NetworkTimeoutSeconds.Should().Be(300);
        agent.RequestsPerMinute.Should().Be(50);
    }
}
