using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Domain.Exceptions;
using FluentAssertions;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0345b/p0349: connections (the p0281a git-host discovery catalog) are a
/// first-class config entity — stored, audited, reverted like the other kinds
/// through the server's DbConfigStore, and connection-scoped project repo refs
/// ("conn/RepoName") validate against them. The fixture is the operator's real
/// shape: connections + connection-scoped project repos, NO legacy repos block.
/// </summary>
public sealed class ConnectionsConfigStoreTests : IDisposable
{
    private readonly DbConfigTestHarness _h = new();
    private static readonly ChangeAttribution Tester = new("tester");

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

    [Fact]
    public void Studio_OperatorShapedConfig_ShowsConnectionsAndResolvedProjects()
    {
        _h.Import(OperatorShapedYaml);
        var catalog = _h.Store.Catalog;

        catalog.Connections.Should().ContainSingle(c =>
            c.Id == "sample-cloud" && c.Type == "azure_devops" && c.Organization == "sample-org"
            && c.Project == "SampleProject" && c.AuthSecret == "ado_token" && c.DefaultBranch == "develop");
        catalog.Repos.Should().BeEmpty("the operator's config declares no legacy repos block");
        catalog.Projects.Should().ContainSingle(p => p.Id == "sample"
            && p.Repos.Contains("sample-cloud/Sample.Api.Server"));
        _h.Store.Invoking(s => s.ExportYaml()).Should().NotThrow();
    }

    [Fact]
    public void Connections_CrudAndAudit_ThroughStore()
    {
        _h.Import(OperatorShapedYaml);

        _h.Store.UpsertConnection(new ConnectionEntity("gh-org", "github", "acme", null, "gh_token", "main"), Tester);
        _h.Store.GetConnections().Should().Contain(c => c.Id == "gh-org" && c.Type == "github"
            && c.Organization == "acme" && c.AuthSecret == "gh_token" && c.DefaultBranch == "main");

        var create = _h.Store.GetChanges().First(c => c.EntityId == "gh-org");
        create.EntityType.Should().Be(ConfigEntityType.Connection);
        create.Operation.Should().Be(ConfigChangeOperation.Create);
        create.Actor.Should().Be("tester");

        _h.Store.UpsertConnection(new ConnectionEntity("gh-org", "github", "acme-2", null, "gh_token", "main"), Tester);
        _h.Store.GetConnections().Single(c => c.Id == "gh-org").Organization.Should().Be("acme-2");
        _h.Store.GetChanges().First(c => c.EntityId == "gh-org").Operation.Should().Be(ConfigChangeOperation.Update);

        _h.Store.DeleteConnection("gh-org", Tester);
        _h.Store.GetConnections().Should().NotContain(c => c.Id == "gh-org");
        _h.Store.GetChanges().First(c => c.EntityId == "gh-org").Operation.Should().Be(ConfigChangeOperation.Delete);
    }

    [Fact]
    public void Revert_ConnectionUpdate_RestoresPriorVersion()
    {
        _h.Import(OperatorShapedYaml);

        _h.Store.UpsertConnection(
            new ConnectionEntity("sample-cloud", "azure_devops", "other-org", "SampleProject", "ado_token", "develop"),
            Tester);
        _h.Store.GetConnections().Single(c => c.Id == "sample-cloud").Organization.Should().Be("other-org");

        var update = _h.Store.GetChanges().First(c =>
            c.EntityId == "sample-cloud" && c.Operation == ConfigChangeOperation.Update);
        _h.Store.Revert(update.Id, new ChangeAttribution("reverter"));

        _h.Store.GetConnections().Single(c => c.Id == "sample-cloud").Organization.Should().Be("sample-org");
    }

    [Fact]
    public void Project_ConnectionScopedRepoRef_ValidWhenConnectionExists()
    {
        _h.Import(OperatorShapedYaml);

        _h.Store.UpsertProject(
            new ProjectEntity("p2", "claude-default", "sample-ado",
                ["sample-cloud/Sample.Worker"], "fix-bug", ["fix-bug"]),
            Tester);

        _h.Store.GetProjects().Should().Contain(p => p.Id == "p2"
            && p.Repos.Single() == "sample-cloud/Sample.Worker");
    }

    [Fact]
    public void Project_UnknownConnectionRef_Rejected()
    {
        _h.Import(OperatorShapedYaml);

        var act = () => _h.Store.UpsertProject(
            new ProjectEntity("broken", "claude-default", "sample-ado",
                ["ghost-conn/Sample.Api.Server"], "fix-bug", ["fix-bug"]),
            Tester);

        act.Should().Throw<ConfigurationException>().WithMessage("*unknown connection 'ghost-conn'*");
        _h.Store.GetProjects().Should().NotContain(p => p.Id == "broken");
    }

    [Fact]
    public void Project_PlainRepoRef_StillValidatesAgainstReposCatalog()
    {
        _h.Import(OperatorShapedYaml);

        var act = () => _h.Store.UpsertProject(
            new ProjectEntity("broken", "claude-default", "sample-ado",
                ["not-a-catalog-repo"], "fix-bug", ["fix-bug"]),
            Tester);

        act.Should().Throw<ConfigurationException>().WithMessage("*unknown repo*");
    }

    [Fact]
    public void UpsertConnection_GithubType_LandsOnOwnerField_AndRoundTrips()
    {
        _h.Import(OperatorShapedYaml);

        _h.Store.UpsertConnection(new ConnectionEntity("gh-org", "github", "acme", null, "gh_token", null), Tester);

        // The exported YAML uses the github field shape (owner:), not organization:.
        var exported = _h.Store.ExportYaml();
        exported.Should().Contain("gh-org:").And.Contain("owner: acme");

        // And the studio reads the org segment back regardless of host kind.
        _h.Store.Load().Connections.Single(c => c.Id == "gh-org").Organization.Should().Be("acme");
    }

    public void Dispose() => _h.Dispose();
}
