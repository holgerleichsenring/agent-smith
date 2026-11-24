using System.Security.Claims;
using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Access;
using AgentSmith.Server.Services.Pocs.Import;
using AgentSmith.Tests.Server.Access;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Pocs;

/// <summary>
/// 2026-11-23-933db: a person's first import creates their reader binding and then their poc- project through the real
/// configuration store, attributed to them; a failure between the two saves is completed by the next import; a stale
/// version is retried once; a lost binding is re-added from the person's Poc rows; a second target's repository joins
/// the person's project.
/// </summary>
public sealed class PocProjectProvisionerTests : IDisposable
{
    private const string Yaml = """
        agents:
          claude:
            type: claude
            model: sonnet-4
        repos:
          poc-src:
            type: github
            url: https://github.com/example/poc-src
            auth: token
          poc-two:
            type: github
            url: https://github.com/example/poc-two
            auth: token
        trackers:
          issues:
            type: github
            url: https://github.com/example/poc-src
            auth: token
        secrets:
          token: ${AGENTSMITH_TEST_TOKEN}
        """;

    private readonly AccessTestHarness _h = new(adminGrant: "sub:rescue-0001");
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _db;
    private readonly PocsConfig _pocs = new() { Enabled = true, ProjectAgent = "claude", ProjectTracker = "issues" };
    private readonly PocTargetConfig _main = new() { Name = "main", SourceRepo = "poc-src" };

    public PocProjectProvisionerTests()
    {
        _db = MigratedStoreTemplate.Context(_connection);
        var raw = new RawConfigYaml().Deserialize(Yaml);
        _h.RawDocStore.Import([.. _h.Assembler.Decompose(raw).Select(d => new ConfigDocWrite(d.Type, d.Id, d.Doc, null, d.Edges, "importer"))], force: true);
        _h.Store.Load();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        _h.Dispose();
    }

    [Fact]
    public async Task FirstImport_WritesTheBindingBeforeTheProject_BothAttributed()
    {
        var provisioned = await Provisioner().EnsureAsync(Ann, _pocs, _main, CancellationToken.None);

        provisioned.Project.Should().Be("poc-ann");
        var project = _h.Store.GetProjects().Single(p => p.Id == "poc-ann");
        (project.Agent, project.Tracker).Should().Be(("claude", "issues"));
        project.Repos.Should().Equal("poc-src");
        var binding = _h.Mapping.Current().Mapping.ProjectBindings.Single(b => b.Project == "poc-ann");
        binding.Roles.Should().Equal(BuiltInRoles.Reader);
        binding.People.Should().ContainSingle().Which.Value.Should().Be("ann-sub");
        var changes = _h.Store.GetChanges().Where(c => c.Actor == "Ann via PoC import").Reverse().Select(c => c.EntityId).ToList();
        changes.Should().Equal(ConfigDocTypes.RoleMapping, "poc-ann");
    }

    [Fact]
    public async Task SecondImport_SamePerson_ChangesNothing()
    {
        await Provisioner().EnsureAsync(Ann, _pocs, _main, CancellationToken.None);
        var before = _h.Store.GetChanges().Count;

        (await Provisioner().EnsureAsync(Ann, _pocs, _main, CancellationToken.None)).Project.Should().Be("poc-ann");

        _h.Store.GetChanges().Should().HaveCount(before);
    }

    [Fact]
    public async Task AFailureAfterTheBinding_IsCompletedByTheNextImport_WithoutASuffix()
    {
        var broken = new PocsConfig { Enabled = true, ProjectAgent = "claude", ProjectTracker = "missing" };
        (await Provisioner().EnsureAsync(Ann, broken, _main, CancellationToken.None)).Refusal.Should().NotBeNull();
        _h.Mapping.Current().Mapping.ProjectBindings.Should().ContainSingle(b => b.Project == "poc-ann");

        var provisioned = await Provisioner().EnsureAsync(Ann, _pocs, _main, CancellationToken.None);

        provisioned.Project.Should().Be("poc-ann");
        _h.Mapping.Current().Mapping.ProjectBindings.Should().ContainSingle(b => b.Project == "poc-ann");
    }

    [Fact]
    public async Task TwoPeople_OnOneReplica_BothKeepTheirBindings()
    {
        await Task.WhenAll(Provisioner().EnsureAsync(Ann, _pocs, _main, CancellationToken.None),
            Provisioner().EnsureAsync(Bob, _pocs, _main, CancellationToken.None));

        _h.Mapping.Current().Mapping.ProjectBindings.Select(b => b.Project).Should().BeEquivalentTo(["poc-ann", "poc-bob"]);
    }

    [Fact]
    public async Task AKeyBoundToSomeoneElse_TakesTheHashSuffix()
    {
        await Provisioner().EnsureAsync(Ann, _pocs, _main, CancellationToken.None);

        var other = await Provisioner().EnsureAsync(User("other-sub", "ann"), _pocs, _main, CancellationToken.None);

        other.Project.Should().MatchRegex("^poc-ann-[0-9a-f]{4}$");
    }

    [Fact]
    public async Task ALostBinding_WithAPocRowOfTheirs_IsReAdded_NoSuffixedProject()
    {
        var poc = PocRows.Poc("todo", "poc-ann");
        poc.Owner = "ann-sub";
        await PocRows.AddAsync(_db, poc);
        _h.Store.UpsertProject(new ProjectEntity("poc-ann", "claude", "issues", ["poc-src"], null, []), new ChangeAttribution("op"));

        (await Provisioner().EnsureAsync(Ann, _pocs, _main, CancellationToken.None)).Project.Should().Be("poc-ann");

        _h.Mapping.Current().Mapping.ProjectBindings.Should().ContainSingle(b => b.Project == "poc-ann");
        _h.Store.GetProjects().Select(p => p.Id).Should().NotContain(id => id.StartsWith("poc-ann-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ASecondTarget_AddsItsSourceRepoToThePersonsProject()
    {
        await Provisioner().EnsureAsync(Ann, _pocs, _main, CancellationToken.None);

        await Provisioner().EnsureAsync(Ann, _pocs, new PocTargetConfig { Name = "two", SourceRepo = "poc-two" }, CancellationToken.None);

        _h.Store.GetProjects().Single(p => p.Id == "poc-ann").Repos.Should().Equal("poc-src", "poc-two");
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task AStaleVersion_IsRetriedOnce_ASecondRefuses(int stale, bool succeeds)
    {
        var store = new Mock<IConfigStore>();
        var thrown = 0;
        store.Setup(s => s.GetProjects()).Returns(() => _h.Store.GetProjects());
        store.Setup(s => s.Load()).Returns(() => _h.Store.Load());
        store.Setup(s => s.UpsertProject(It.IsAny<ProjectEntity>(), It.IsAny<ChangeAttribution>()))
            .Callback<ProjectEntity, ChangeAttribution>((entity, by) =>
            {
                if (thrown++ < stale) throw new StaleConfigVersionException("project", "poc-ann", 1, 2);
                _h.Store.UpsertProject(entity, by);
            });

        var provisioned = await Provisioner(store.Object).EnsureAsync(Ann, _pocs, _main, CancellationToken.None);

        (provisioned.Project is not null).Should().Be(succeeds);
        if (!succeeds) provisioned.Refusal.Should().Contain("configuration changed");
    }

    private static ClaimsPrincipal Ann => User("ann-sub", "ann");

    private static ClaimsPrincipal Bob => User("bob-sub", "bob");

    private static ClaimsPrincipal User(string sub, string preferred) => new(new ClaimsIdentity(
    [
        new Claim("sub", sub), new Claim(ClaimTypes.Name, sub), new Claim("preferred_username", preferred),
        new Claim("name", char.ToUpperInvariant(preferred[0]) + preferred[1..]),
    ], "test"));

    private PocProjectProvisioner Provisioner(IConfigStore? store = null) => new(
        store ?? _h.Store, _h.Mapping, _h.Writer, _h.Auth,
        new PocImportRepository(_db, new SqliteUniqueViolationTranslator()), NullLogger<PocProjectProvisioner>.Instance);
}
