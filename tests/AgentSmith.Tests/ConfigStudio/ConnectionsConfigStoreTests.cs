using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0345b: connections (the p0281a git-host discovery catalog) are a first-class
/// config entity — stored, audited, reverted like the other kinds, and the
/// entity connection-scoped project repo refs ("conn/RepoName") validate
/// against. The fixture is shaped like the operator's real config: connections
/// + connection-scoped project repos, NO legacy repos block.
/// </summary>
public sealed class ConnectionsConfigStoreTests : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private static readonly ChangeAttribution Tester = new("tester");

    private sealed record FixedLocation(string ConfigPath) : IConfigStoreLocation;

    // The operator's world: discovery connections, conn-scoped repos, no repos: block.
    private const string OperatorShapedYaml = """
        agents:
          claude-default:
            type: claude
            model: sonnet-4
        connections:
          sample-cloud:
            type: azure_devops
            organization: sample-org
            project: SampleProject
            auth: ado_token
            default_branch: develop
        trackers:
          sample-ado:
            type: azure_devops
            organization: sample-org
            project: SampleProject
            auth: ado_token
        projects:
          sample:
            agent: claude-default
            tracker: sample-ado
            repos: [sample-cloud/Sample.Api.Server, sample-cloud/Sample.Web.Client]
            pipeline: fix-bug
        secrets:
          ado_token: ${AGENTSMITH_TEST_ADO_TOKEN}
        """;

    private (FileConfigStore Store, InMemoryConfigAuditStore Audit, string Path) NewStore(string yaml)
    {
        var path = Path.Combine(Path.GetTempPath(), $"agentsmith-conn-{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, yaml);
        _tempFiles.Add(path);
        var audit = new InMemoryConfigAuditStore();
        var store = new FileConfigStore(new FixedLocation(path), audit, NullLogger<FileConfigStore>.Instance);
        store.Load();
        return (store, audit, path);
    }

    // p0345b spec test: Studio_OperatorShapedConfig_ShowsConnectionsAndResolvedProjects
    [Fact]
    public void Studio_OperatorShapedConfig_ShowsConnectionsAndResolvedProjects()
    {
        var (store, _, _) = NewStore(OperatorShapedYaml);
        var catalog = store.Catalog;

        catalog.Connections.Should().ContainSingle(c =>
            c.Id == "sample-cloud" && c.Type == "azure_devops" && c.Organization == "sample-org"
            && c.Project == "SampleProject" && c.AuthSecret == "ado_token" && c.DefaultBranch == "develop");
        catalog.Repos.Should().BeEmpty("the operator's config declares no legacy repos block");
        catalog.Projects.Should().ContainSingle(p => p.Id == "sample"
            && p.Repos.Contains("sample-cloud/Sample.Api.Server"));

        // Nothing falsely dangling: the full-catalog validation (the export gate)
        // accepts connection-scoped refs because the connection exists.
        var act = () => store.ExportYaml();
        act.Should().NotThrow();
    }

    // p0345b spec test: Connections_CrudAndAudit_ThroughStoreAndApi (store half;
    // the API half lives in ConfigStudioApiSmokeTests).
    [Fact]
    public void Connections_CrudAndAudit_ThroughStore()
    {
        var (store, audit, path) = NewStore(OperatorShapedYaml);

        // Create
        store.UpsertConnection(
            new ConnectionEntity("gh-org", "github", "acme", null, "gh_token", "main"), Tester);
        store.GetConnections().Should().Contain(c => c.Id == "gh-org" && c.Type == "github"
            && c.Organization == "acme" && c.AuthSecret == "gh_token" && c.DefaultBranch == "main");

        var create = audit.GetAll().First(c => c.EntityId == "gh-org");
        create.EntityType.Should().Be(ConfigEntityType.Connection);
        create.Operation.Should().Be(ConfigChangeOperation.Create);
        create.Actor.Should().Be("tester");

        // Update
        store.UpsertConnection(
            new ConnectionEntity("gh-org", "github", "acme-2", null, "gh_token", "main"), Tester);
        store.GetConnections().Single(c => c.Id == "gh-org").Organization.Should().Be("acme-2");
        audit.GetAll().First(c => c.EntityId == "gh-org").Operation.Should().Be(ConfigChangeOperation.Update);

        // The mutation is persisted to the file and reloadable.
        var reloaded = new FileConfigStore(new FixedLocation(path), new InMemoryConfigAuditStore(),
            NullLogger<FileConfigStore>.Instance);
        reloaded.Load().Connections.Should().Contain(c => c.Id == "gh-org" && c.Organization == "acme-2");

        // Delete
        store.DeleteConnection("gh-org", Tester);
        store.GetConnections().Should().NotContain(c => c.Id == "gh-org");
        audit.GetAll().First(c => c.EntityId == "gh-org").Operation.Should().Be(ConfigChangeOperation.Delete);
    }

    [Fact]
    public void Revert_ConnectionUpdate_RestoresPriorVersion()
    {
        var (store, audit, _) = NewStore(OperatorShapedYaml);

        store.UpsertConnection(
            new ConnectionEntity("sample-cloud", "azure_devops", "other-org", "SampleProject", "ado_token", "develop"),
            Tester);
        store.GetConnections().Single(c => c.Id == "sample-cloud").Organization.Should().Be("other-org");

        var update = audit.GetAll().First(c =>
            c.EntityId == "sample-cloud" && c.Operation == ConfigChangeOperation.Update);
        store.Revert(update.Id, new ChangeAttribution("reverter"));

        store.GetConnections().Single(c => c.Id == "sample-cloud").Organization.Should().Be("sample-org");
    }

    // p0345b spec test: Project_ConnectionScopedRepoRef_ValidWhenConnectionExists
    [Fact]
    public void Project_ConnectionScopedRepoRef_ValidWhenConnectionExists()
    {
        var (store, _, _) = NewStore(OperatorShapedYaml);

        store.UpsertProject(
            new ProjectEntity("p2", "claude-default", "sample-ado",
                ["sample-cloud/Sample.Worker"], "fix-bug", ["fix-bug"]),
            Tester);

        store.GetProjects().Should().Contain(p => p.Id == "p2"
            && p.Repos.Single() == "sample-cloud/Sample.Worker");
    }

    // p0345b spec test: Project_UnknownConnectionRef_Rejected400 (store half —
    // the ConfigurationException the API surfaces as HTTP 400).
    [Fact]
    public void Project_UnknownConnectionRef_Rejected()
    {
        var (store, _, _) = NewStore(OperatorShapedYaml);

        var act = () => store.UpsertProject(
            new ProjectEntity("broken", "claude-default", "sample-ado",
                ["ghost-conn/Sample.Api.Server"], "fix-bug", ["fix-bug"]),
            Tester);

        act.Should().Throw<ConfigurationException>().WithMessage("*unknown connection 'ghost-conn'*");
        store.GetProjects().Should().NotContain(p => p.Id == "broken");
    }

    [Fact]
    public void Project_PlainRepoRef_StillValidatesAgainstReposCatalog()
    {
        var (store, _, _) = NewStore(OperatorShapedYaml);

        var act = () => store.UpsertProject(
            new ProjectEntity("broken", "claude-default", "sample-ado",
                ["not-a-catalog-repo"], "fix-bug", ["fix-bug"]),
            Tester);

        act.Should().Throw<ConfigurationException>().WithMessage("*unknown repo*");
    }

    [Fact]
    public void UpsertConnection_GithubType_LandsOnOwnerField_AndRoundTrips()
    {
        var (store, _, path) = NewStore(OperatorShapedYaml);

        store.UpsertConnection(
            new ConnectionEntity("gh-org", "github", "acme", null, "gh_token", null), Tester);

        // The written YAML uses the github field shape (owner:), not organization:.
        var written = File.ReadAllText(path);
        written.Should().Contain("gh-org:");
        written.Should().Contain("owner: acme");

        // And the studio reads the org segment back regardless of host kind.
        var reloaded = new FileConfigStore(new FixedLocation(path), new InMemoryConfigAuditStore(),
            NullLogger<FileConfigStore>.Instance);
        reloaded.Load().Connections.Single(c => c.Id == "gh-org").Organization.Should().Be("acme");
    }

    public void Dispose()
    {
        foreach (var f in _tempFiles)
            if (File.Exists(f)) File.Delete(f);
    }
}
