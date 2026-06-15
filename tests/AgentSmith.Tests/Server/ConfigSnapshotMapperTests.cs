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
            Url = "https://acme.atlassian.net",
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
                    Polling = new PollingConfig { Enabled = true, IntervalSeconds = 120 },
                    JiraTrigger = new JiraTriggerConfig
                    {
                        TriggerStatuses = ["To Do", "In Progress"],
                        DoneStatus = "Done", FailedStatus = "Failed", CommentKeyword = "@agentsmith",
                    },
                },
            },
        };
    }

    // p0270a: the mapper now also projects each project's resolved settings from
    // the single IConfigResolver. Stub resolvers keep these mapper tests focused
    // on the redaction + reachability allow-list (resolution itself is pinned in
    // ConfigResolutionPassTests).
    private static ConfigSnapshot Snap(AgentSmithConfig config) =>
        ConfigSnapshotMapper.ToSnapshot(config, new AgentSmith.Application.Services.Configuration.ConfigResolutionPass(
            Microsoft.Extensions.Options.Options.Create(new SandboxGlobalConfig()),
            new AgentSmith.Tests.Sandbox.StubSandboxResourceResolver(),
            new AgentSmith.Tests.Sandbox.StubAgentImageResolver(),
            new AgentSmith.Tests.Sandbox.StubOrchestratorImageResolver(),
            config));

    [Fact]
    public void ToSnapshot_WithSecrets_OmitsApiKeysTokensAndConnectionStrings()
    {
        var snapshot = Snap(BuildConfig());

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
        var snapshot = Snap(BuildConfig());

        snapshot.Edges.Should().ContainEquivalentOf(new ConfigEdge("ops", "claude", "agent"));
        snapshot.Edges.Should().ContainEquivalentOf(new ConfigEdge("ops", "acme-jira", "tracker"));
        snapshot.Edges.Should().ContainEquivalentOf(new ConfigEdge("ops", "sample-server", "repo"));
        snapshot.Edges.Should().ContainEquivalentOf(new ConfigEdge("ops", "fix-bug", "pipeline"));
        snapshot.Edges.Should().ContainEquivalentOf(new ConfigEdge("ops", "security-scan", "pipeline"));
    }

    [Fact]
    public void ConfigSnapshotMapper_Repo_EmitsFullUrlAndOrgProject_NoAuth()
    {
        var snapshot = Snap(BuildConfig());

        var repo = snapshot.Repos.Should().ContainSingle().Subject;
        // p0271: full URL surfaced (operator decision — URL is not sensitive here).
        repo.Url.Should().Be("https://github.com/acme/sample-server.git");
        repo.DefaultBranch.Should().Be("main");
        // The auth secret is still never on the wire.
        JsonSerializer.Serialize(repo).Should().NotContain(SecretRepoAuth);
    }

    [Fact]
    public void ConfigSnapshotMapper_Tracker_EmitsUrlAndTriggerConfig()
    {
        var snapshot = Snap(BuildConfig());

        var tracker = snapshot.Trackers.Should().ContainSingle().Subject;
        tracker.Url.Should().Be("https://acme.atlassian.net");
        JsonSerializer.Serialize(tracker).Should().NotContain(SecretTrackerAuth);

        var trigger = snapshot.Projects.Should().ContainSingle().Subject.Trigger;
        trigger.TriggerStatuses.Should().Contain("In Progress");
        trigger.DoneStatus.Should().Be("Done");
        trigger.FailedStatus.Should().Be("Failed");
        trigger.PollingEnabled.Should().BeTrue();
        trigger.PollingIntervalSeconds.Should().Be(120);
        trigger.CommentKeyword.Should().Be("@agentsmith");
    }

    [Fact]
    public void ToSnapshot_Globals_MapsSandboxOrchestratorLimitCostCap()
    {
        var config = BuildConfig();
        config.Sandbox.AgentVersion = "0.48.0";
        config.Sandbox.StepTimeoutSeconds = 900;

        var globals = Snap(config).Globals;

        globals.Sandbox.AgentVersion.Should().Be("0.48.0");
        globals.Sandbox.StepTimeoutSeconds.Should().Be(900);
        globals.Orchestrator.MaxRunWallTimeSeconds.Should().Be(1800);
        globals.CostCap.Usd.Should().Be(5.0m);
        globals.Limits.MaxToolCallsPerSkill.Should().Be(30);
    }

    [Fact]
    public void ToSnapshot_Agent_KeepsTuningFieldsByCatalogName()
    {
        var agent = Snap(BuildConfig()).Agents.Should().ContainSingle().Subject;

        agent.Name.Should().Be("claude");
        agent.Model.Should().Be("claude-opus-4-1");
        agent.NetworkTimeoutSeconds.Should().Be(300);
        agent.RequestsPerMinute.Should().Be(50);
    }
}
