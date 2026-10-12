using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.MultiRepo;

/// <summary>
/// 2026-10-12-f5f7d: each repository a ticket's specs name gets its own sub-series in ONE write — its
/// specs, a manifest listing only them, one revision time and one member list for every member — and
/// no repository is picked by position.
/// </summary>
public sealed class SeriesPerRepoWriteTests
{
    private static readonly RepoConnection Worker = new() { Name = "BackgroundWorker" };
    private static readonly RepoConnection Server = new() { Name = "Server" };
    private static readonly RepoConnection Client = new() { Name = "Client" };
    private readonly SeriesRepoBranches _branches = new();

    [Fact]
    public async Task SeriesWriter_SpecForServer_SubSeriesInServerOnly()
    {
        _branches.Repo(Worker.Name);
        _branches.Repo(Server.Name);

        var write = await WriteAsync(Set(("a", "Server")), Worker, Server);

        write.Written.Should().BeTrue(write.Error);
        write.Shas.Keys.Should().Equal("Server");
        (await FilesAsync(Worker.Name)).Should().BeEmpty("the spec changes nothing in the worker");
        (await FilesAsync(Server.Name)).Should().BeEquivalentTo(
            SeriesPaths.Manifest(SeriesRepoBranches.Base), $"{SeriesPaths.Planned}/{Id('a')}-a.yaml");
        Manifest(Server.Name).SeriesRepos.Should().Equal("Server");
    }

    [Fact]
    public async Task SeriesWriter_TwoMembers_SameStampAndMembers()
    {
        _branches.Repo(Server.Name);
        _branches.Repo(Client.Name);

        var write = await WriteAsync(Set(("a", "Server"), ("b", "Client")), Server, Client);

        write.Written.Should().BeTrue(write.Error);
        var server = Manifest(Server.Name);
        var client = Manifest(Client.Name);
        server.Specs.Should().Equal(Id('a'));
        client.Specs.Should().Equal(Id('b'));
        client.Revisions[^1].At.Should().Be(server.Revisions[^1].At, "one write, one revision time");
        client.SeriesRepos.Should().Equal(server.SeriesRepos).And.Equal("Server", "Client");
    }

    [Fact]
    public async Task SeriesWriter_NamedRepoNotCheckedOut_Refuses()
    {
        _branches.Repo(Server.Name);
        var set = Set(("a", "Server"), ("b", "Client"));
        var placement = new SeriesPlacement(set with { SeriesRepos = ["Server", "Client"] }, [Server, Client], Server);

        var write = await SeriesDoubles.Writer(_branches.Files()).WriteAsync(_branches.Pipeline(), placement, default);

        write.Written.Should().BeFalse();
        write.IsRefused.Should().BeTrue();
        write.Error.Should().Contain("'Client'").And.Contain("not checked out");
        (await FilesAsync(Server.Name)).Should().BeEmpty("a refused write writes nothing anywhere");
    }

    [Fact]
    public void SeriesRepos_LegacySpecWithoutRepo_BelongsToCarrier()
    {
        var set = Set(("a", null), ("b", "Server"));

        var placement = new SeriesRepos().Place(set, new SeriesScope([Worker, Server], "BackgroundWorker", [], "BackgroundWorker"));

        placement.Refusal.Should().BeNull();
        placement.Set.Specs.Select(p => p.Draft.Repos.Single()).Should().Equal("BackgroundWorker", "Server");
        placement.Names.Should().Equal("BackgroundWorker", "Server");
        placement.Carrier!.Name.Should().Be("BackgroundWorker", "the earliest spec's repository, in series order");
    }

    [Fact]
    public void SeriesRepos_SpecNamingSeveral_IsRefusedWithItsId()
    {
        var set = Set(("a", "Server"));
        set = set with { Specs = [set.Specs[0] with { Draft = set.Specs[0].Draft with { Repos = ["Server", "Client"] } }] };

        var placement = new SeriesRepos().Place(set, new SeriesScope([Server, Client], null, [], null));

        placement.Refusal.Should().Contain(Id('a')).And.Contain("several repositories");
    }

    [Fact]
    public void SeriesRepos_SpecNamingARepositoryOutsideTheRun_IsRefusedWithItsName()
    {
        var placement = new SeriesRepos().Place(Set(("a", "Client")), new SeriesScope([Server], null, [], null));

        placement.Refusal.Should().Contain("'Client'").And.Contain("not checked out");
    }

    [Fact]
    public void SeriesRepos_SetWithoutSpecs_GoesToTheMembersReadAndKeepsTheCarrier()
    {
        var empty = Set() with { Handback = new SpecHandback(SpecHandbackCase.Question, "which one?") };

        var read = new SeriesRepos().Place(empty, new SeriesScope([Worker, Server, Client], null, ["Client", "Server"], "Server"));
        var scoped = new SeriesRepos().Place(empty, new SeriesScope([Worker, Server], null, [], null));

        read.Names.Should().Equal("Client", "Server");
        read.Carrier!.Name.Should().Be("Server", "a set without specs keeps the carrier it had");
        scoped.Names.Should().Equal("BackgroundWorker", "Server");
        scoped.Carrier.Should().BeNull();
    }

    [Fact]
    public void SeriesRepos_CarrierOf_IsTheEarliestDraftsRepositoryElseTheOnlyOne()
    {
        var repos = new SeriesRepos();
        SpecDraft[] drafts = [Draft("a", null), Draft("b", "Client"), Draft("c", "Server")];

        repos.CarrierOf(drafts, ["Server", "Client"]).Should().Be("Client");
        repos.CarrierOf([Draft("a", null)], ["Server"]).Should().Be("Server");
        repos.CarrierOf([Draft("a", null)], ["Server", "Client"]).Should().BeEmpty("no position decides");
    }

    [Fact]
    public void SpecSet_OfRepository_KeepsOnlyItsSpecsAndTheirExecutedIds()
    {
        var set = Set(("a", "Server"), ("b", "Client")) with { Executed = [Id('a')] };

        var server = set.OfRepository("server");

        server.Specs.Select(p => p.SpecId).Should().Equal(Id('a'));
        server.Executed.Should().Equal(Id('a'));
        set.OfRepository("Client").Executed.Should().BeEmpty();
    }

    private async Task<SpecSetWriteResult> WriteAsync(SpecSet set, params RepoConnection[] run) =>
        await SeriesDoubles.Writer(_branches.Files()).WriteAsync(
            _branches.Pipeline(), new SeriesRepos().Place(set, new SeriesScope(run, null, [], null)), default);

    private async Task<IReadOnlyList<string>> FilesAsync(string repo) =>
        await _branches.Repo(repo).ListAsync(".agentsmith", null, default);

    private SeriesManifestDocument Manifest(string repo) =>
        new SeriesManifest().Parse(_branches.Repo(repo).TryReadAsync(
            SeriesPaths.Manifest(SeriesRepoBranches.Base), default).Result)!;

    private static string Id(char letter) => SeriesRepoBranches.Id(letter);

    private static SpecDraft Draft(string letter, string? repo) =>
        new(Id(letter[0]), $"Goal {letter}", $"spec: {Id(letter[0])}\ngoal: g\n", [])
        {
            Done = ["Done."], Repos = repo is null ? [] : [repo],
        };

    internal static SpecSet Set(params (string Letter, string? Repo)[] specs) => new(
        SeriesRepoBranches.Key,
        [.. specs.Select(s => new DerivedSpec(Draft(s.Letter, s.Repo), s.Letter, string.Empty, []))],
        SpecAccounting.Empty,
        [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UtcNow)],
        SpecSource.Derived,
        Series: SeriesRepoBranches.Base);
}
