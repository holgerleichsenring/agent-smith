using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Configuration;

/// <summary>
/// p0515: a configured NAME denotes one entity however it is capitalised. Measured on a
/// live deployment: the init endpoint accepted a project whose configured key carried
/// capitals (it matched the key exactly), and the queue consumer then failed with
/// "Project '&lt;lowercased&gt;' not found in configuration" — two lookup rules for one name.
/// Every config here comes from the real loader pipeline, never a hand-built
/// <see cref="AgentSmithConfig"/>: the comparer lives in the CATALOG, so a config assembled
/// by hand would prove nothing.
/// </summary>
public sealed class CaseInsensitiveConfigIdentityTests
{
    [Fact]
    public void LoadConfig_ACatalogKeyWithCapitals_IsFoundUnderAnyCasing()
    {
        var resolved = Resolve("""
            agents:
              Claude-Default: { type: Claude }
            repos:
              Service.Api: { type: GitHub, url: https://x, auth: t }
            connections:
              Acme: { type: GitHub, owner: acme, auth: t }
            trackers:
              Board: { type: GitHub, auth: t }
            projects:
              Demo: { agent: Claude-Default, tracker: Board, repos: [Service.Api] }
            """);

        resolved.Findings.Should().BeEmpty();
        resolved.Config.Projects.Should().ContainKey("demo");
        resolved.Config.Agents.Should().ContainKey("CLAUDE-DEFAULT");
        resolved.Config.Repos.Should().ContainKey("service.api");
        resolved.Config.Connections.Should().ContainKey("ACME");
        resolved.Config.Trackers.Should().ContainKey("board");
    }

    [Fact]
    public void LoadConfig_AnAgentRefDifferingOnlyInCase_ResolvesInTheProjectBuilder()
    {
        // The agents catalog used to be handed to the project builder as the RAW map while
        // the composed config got the same raw map — so a differently cased agent ref failed
        // resolution here even once every other catalog matched case-insensitively.
        var resolved = Resolve("""
            agents:
              Claude-Default: { type: Claude }
            repos:
              r: { type: GitHub, url: https://x, auth: t }
            trackers:
              t: { type: GitHub, auth: t }
            projects:
              demo:
                agent: claude-default
                tracker: T
                repos: [R]
                pipelines:
                  - { name: code, agent: CLAUDE-DEFAULT }
            """);

        resolved.Findings.Should().BeEmpty();
        var project = resolved.Config.Projects["demo"];
        project.Agent.Type.Should().Be("Claude");
        project.Tracker.Name.Should().Be("t");
        project.Repos.Should().ContainSingle().Which.Name.Should().Be("r");
        project.Pipelines.Should().ContainSingle().Which.Agent.Should().NotBeNull();
    }

    [Fact]
    public void LoadConfig_ATrackerRefWithCapitals_StillMergesTheTrackersWorkflow()
    {
        // The effective-trigger merge does a KEYED lookup on the raw trackers map, before any
        // catalog exists. A miss there is silent: the project would keep an EMPTY
        // trigger_statuses, which the poller reads as "every status matches".
        var resolved = Resolve("""
            agents:
              a: { type: Claude }
            repos:
              r: { type: GitHub, url: https://x, auth: t }
            trackers:
              Board:
                type: GitHub
                auth: t
                trigger_statuses: [ready]
            projects:
              demo:
                agent: a
                tracker: board
                repos: [r]
                github_trigger: { }
            """);

        resolved.Findings.Should().BeEmpty();
        resolved.Config.Projects["demo"].GithubTrigger!.TriggerStatuses
            .Should().BeEquivalentTo(["ready"]);
    }

    [Fact]
    public void LoadConfig_ATrackerStatusMap_StaysCaseSensitive()
    {
        // A tracker's lifecycle status names are the PROVIDER's names, not this
        // configuration's, so their case is not ours to normalize.
        var resolved = Resolve("""
            agents:
              a: { type: Claude }
            repos:
              r: { type: GitHub, url: https://x, auth: t }
            trackers:
              t:
                type: GitHub
                auth: t
                lifecycle_status_names: { InProgress: "In Progress" }
            projects:
              demo: { agent: a, tracker: t, repos: [r] }
            """);

        var names = resolved.Config.Trackers["t"].LifecycleStatusNames;
        names.Should().ContainKey("InProgress");
        names.Should().NotContainKey("inprogress");
    }

    [Fact]
    public void LoadConfig_TwoKeysDifferingOnlyInCase_DropBothAndTheFindingNamesTheCatalogAndBoth()
    {
        var resolved = Resolve("""
            agents:
              a: { type: Claude }
            repos:
              Service.Api: { type: GitHub, url: https://x, auth: t }
              service.api: { type: GitHub, url: https://y, auth: t }
            trackers:
              t: { type: GitHub, auth: t }
            projects:
              demo: { agent: a, tracker: t, repos: [Service.Api] }
            """);

        resolved.Config.Repos.Should().BeEmpty("neither half of a colliding pair is loaded");
        var collision = resolved.Findings.Should()
            .ContainSingle(f => f.Field == "repos:Service.Api").Subject;
        collision.IsBlocking.Should().BeTrue();
        collision.Project.Should().BeNull("a colliding repo name must not silence a project of that name");
        collision.Reason.Should().Contain("'Service.Api'").And.Contain("'service.api'");
        collision.Reason.Should().Contain("repos");

        // The cascade is the point: the project that referenced the dropped repo raises its
        // own finding on top, so the operator sees the collision AND what it took down.
        resolved.Config.Projects.Should().NotContainKey("demo");
        resolved.Findings.Should().ContainSingle(f => f.Project == "demo" && f.Field == "repos");
    }

    [Fact]
    public void LoadConfig_ACollisionInTwoCatalogs_ProducesTwoFindings()
    {
        // A finding's identity is Subsystem|Project|Trigger|Field and the findings list
        // dedupes on it — two collisions carrying the same slots would collapse into one.
        var resolved = Resolve("""
            agents:
              Shared: { type: Claude }
              shared: { type: Claude }
            repos:
              Shared: { type: GitHub, url: https://x, auth: t }
              shared: { type: GitHub, url: https://y, auth: t }
            trackers:
              t: { type: GitHub, auth: t }
            projects:
              demo: { agent: Shared, tracker: t, repos: [Shared] }
            """);

        var collisions = resolved.Findings.Where(f => f.Project is null).ToList();
        collisions.Select(f => f.Field).Should().BeEquivalentTo(["agents:Shared", "repos:Shared"]);
        resolved.Config.Agents.Should().BeEmpty();
        resolved.Config.Repos.Should().BeEmpty();
    }

    [Fact]
    public void LoadConfig_TwoProjectKeysDifferingOnlyInCase_DropBoth()
    {
        var resolved = Resolve("""
            agents:
              a: { type: Claude }
            repos:
              r: { type: GitHub, url: https://x, auth: t }
            trackers:
              t: { type: GitHub, auth: t }
            projects:
              Demo: { agent: a, tracker: t, repos: [r] }
              demo: { agent: a, tracker: t, repos: [r] }
            """);

        resolved.Config.Projects.Should().BeEmpty();
        resolved.Findings.Should().ContainSingle(f => f.Field == "projects:Demo" && f.IsBlocking);
    }

    [Fact]
    public void ValidateProject_ARefDifferingOnlyInCase_IsAccepted()
    {
        // The editor has to agree with the loader, or the studio saves a wiring the boot
        // refuses — the split this phase exists to close.
        var catalog = Catalog(agent: "Claude-Default", tracker: "Board", repo: "Service.Api");
        var project = new ProjectEntity(
            "demo", "claude-default", "board", ["service.api"], null, []);

        var act = () => ConfigReferentialValidator.ValidateProject(project, catalog);

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateProject_ARefMatchingBothHalvesOfACollidingPair_IsRejected()
    {
        // The loader drops both halves, so accepting the reference here would promise a
        // wiring that never materializes.
        var catalog = new ConfigCatalog(
            [Agent("Shared"), Agent("shared")],
            [new TrackerEntity("board", "github", null)],
            [new RepoEntity("repo", "https://x", null)],
            [], [], [], []);
        var project = new ProjectEntity("demo", "shared", "board", ["repo"], null, []);

        var act = () => ConfigReferentialValidator.ValidateProject(project, catalog);

        act.Should().Throw<ConfigurationException>().WithMessage("*ambiguous agent 'shared'*");
    }

    [Fact]
    public void ValidateProject_AnUnknownRef_IsStillRejected()
    {
        var catalog = Catalog(agent: "a", tracker: "t", repo: "r");
        var project = new ProjectEntity("demo", "ghost", "t", ["r"], null, []);

        var act = () => ConfigReferentialValidator.ValidateProject(project, catalog);

        act.Should().Throw<ConfigurationException>().WithMessage("*unknown agent 'ghost'*");
    }

    private static AgentEntity Agent(string id) => new() { Id = id, Provider = "claude" };

    private static ConfigCatalog Catalog(string agent, string tracker, string repo) => new(
        [Agent(agent)],
        [new TrackerEntity(tracker, "github", null)],
        [new RepoEntity(repo, "https://x", null)],
        [], [], [], []);

    private static (AgentSmithConfig Config, IReadOnlyList<StartupFinding> Findings) Resolve(string yaml)
    {
        var materializer = new RawConfigMaterializer(
            new ProjectConfigNormalizer(),
            new EffectiveTriggerBuilder(),
            new DeploymentDefaultsApplier(),
            new ConfigCatalogResolver(),
            new AgentSmithPaths());
        var config = materializer.Materialize(new RawConfigYaml().Deserialize(yaml));
        return (config, materializer.LastResolutionFindings);
    }
}
