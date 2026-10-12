using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.MultiRepo;

/// <summary>
/// 2026-10-12-f5f7b: every run repository may hold its own self-contained sub-series of a ticket;
/// the reader joins them by the newest manifest's members, in id order, all or nothing — and a
/// series written by one repository reads exactly as before.
/// </summary>
public sealed class SeriesMultiRepoReadTests
{
    private static readonly string[] Both = ["Server", "Client"];
    private readonly SeriesRepoBranches _branches = new();

    [Fact]
    public async Task SeriesReader_SubSeriesInTwoRepos_JoinsById()
    {
        _branches.SubSeries("Server", SeriesRepoBranches.T2, Both, [Id('a'), Id('c')],
            new Dictionary<string, string> { [Id('c')] = Id('b') });
        _branches.SubSeries("Client", SeriesRepoBranches.T2, Both, [Id('b')],
            new Dictionary<string, string> { [Id('b')] = Id('a') });

        var result = await _branches.ReadAsync("Server", Both);

        result.State.Should().Be(SpecSetBranchState.Answered);
        result.Set!.Phases.Select(p => p.PhaseId).Should().Equal(Id('a'), Id('b'), Id('c'));
        result.Read!.RevisionShas.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["Server"] = "sha-Server", ["Client"] = "sha-Client" });
        result.Read.LastCommitSha.Should().Be("sha-Server", "the carrying repository's sha stays the one sha");
        result.Read.Lagging.Should().BeEmpty();
    }

    [Fact]
    public async Task SeriesReader_BackwardRequires_IsUnreadable()
    {
        _branches.SubSeries("Server", SeriesRepoBranches.T2, Both, [Id('a')],
            new Dictionary<string, string> { [Id('a')] = Id('b') });
        _branches.SubSeries("Client", SeriesRepoBranches.T2, Both, [Id('b')]);

        var result = await _branches.ReadAsync("Server", Both);

        result.State.Should().Be(SpecSetBranchState.Unreadable);
        result.Why.Should().Contain($"{Id('a')} requires {Id('b')}");
    }

    [Fact]
    public async Task SeriesReader_ExecutedGapAcrossRepos_IsUnreadable()
    {
        _branches.SubSeries("Server", SeriesRepoBranches.T2, Both, [Id('a')]);
        _branches.SubSeries("Client", SeriesRepoBranches.T2, Both, [Id('b')], executed: Id('b'));

        var result = await _branches.ReadAsync("Server", Both);

        result.State.Should().Be(SpecSetBranchState.Unreadable);
        result.Why.Should().Contain(Id('b')).And.Contain(Id('a')).And.Contain("not a prefix");
    }

    [Fact]
    public async Task SeriesReader_ExecutedPrefixAcrossRepos_ReadsAsExecuted()
    {
        _branches.SubSeries("Server", SeriesRepoBranches.T2, Both, [Id('a')], executed: Id('a'));
        _branches.SubSeries("Client", SeriesRepoBranches.T2, Both, [Id('b')]);

        var result = await _branches.ReadAsync("Server", Both);

        result.Set!.Executed.Should().Equal(Id('a'));
        result.Set.ExecutedHead.Select(p => p.PhaseId).Should().Equal(Id('a'));
    }

    [Fact]
    public async Task SeriesReader_ManifestWithoutParsedTime_NeverNewest()
    {
        _branches.SubSeries("Server", SeriesRepoBranches.T1, ["Server"], [Id('a')]);
        _branches.SubSeries("Client", "not a time", ["Client"], [Id('b')]);

        var result = await _branches.ReadAsync("Client", Both);

        result.State.Should().Be(SpecSetBranchState.Answered);
        result.Set!.Phases.Select(p => p.PhaseId).Should().Equal([Id('a')],
            "an unparsed time is no claim to be newest, even in the carrying repository");
    }

    [Fact]
    public async Task SeriesReader_NamedMemberNotCheckedOut_IsUnreadable()
    {
        _branches.SubSeries("Server", SeriesRepoBranches.T2, Both, [Id('a')]);

        var result = await _branches.ReadAsync("Server", Both);

        result.State.Should().Be(SpecSetBranchState.Unreadable);
        result.Why.Should().Contain("'Client'").And.Contain("not checked out");
    }

    [Fact]
    public async Task SeriesReader_NamedMemberWithNothing_IsUnreadable()
    {
        _branches.SubSeries("Server", SeriesRepoBranches.T2, Both, [Id('a')]);
        _branches.Repo("Client");

        var result = await _branches.ReadAsync("Server", Both);

        result.State.Should().Be(SpecSetBranchState.Unreadable);
        result.Why.Should().Contain("'Client'").And.Contain("no sub-series");
    }

    [Fact]
    public async Task SeriesReader_StaleSubSeriesOutsideNewestMembers_Ignored()
    {
        _branches.SubSeries("Server", SeriesRepoBranches.T2, ["Server"], [Id('a')]);
        _branches.SubSeries("Client", SeriesRepoBranches.T1, Both, [Id('b')]);

        var result = await _branches.ReadAsync("Server", Both);

        result.Set!.Phases.Select(p => p.PhaseId).Should().Equal([Id('a')]);
        result.Read!.Lagging.Should().BeEmpty("a repository the newest manifest does not name is no member");
        result.Read.RevisionShas.Keys.Should().Equal("Server");
    }

    [Fact]
    public async Task SeriesReader_MemberLagging_ReturnsNewestSetAndNamesLagging()
    {
        _branches.SubSeries("Server", SeriesRepoBranches.T2, Both, [Id('a')]);
        _branches.SubSeries("Client", SeriesRepoBranches.T1, Both, [Id('b')]);

        var result = await _branches.ReadAsync("Server", Both);

        result.State.Should().Be(SpecSetBranchState.Answered);
        result.Set!.Phases.Select(p => p.PhaseId).Should().Equal([Id('a')], "a lagging member is never mixed in");
        result.Read!.Lagging.Should().Equal("Client");
        result.Read.RevisionShas.Keys.Should().Equal("Server");
    }

    [Fact]
    public async Task SeriesReader_LegacyManifestInTwoRepos_ReadsPointerRepo()
    {
        _branches.SubSeries("Server", SeriesRepoBranches.T2, null, [Id('a')]);
        _branches.SubSeries("Client", SeriesRepoBranches.T1, null, ["2026-10-12-0c0ca"], seriesBase: "2026-10-12-0c0c");

        var result = await _branches.ReadAsync("Client", Both);

        result.Set!.Series.Should().Be("2026-10-12-0c0c");
        result.Read!.RevisionShas.Keys.Should().Equal("Client");
    }

    [Fact]
    public async Task SeriesReader_MembersOnDifferentBases_IsUnreadable()
    {
        _branches.SubSeries("Server", SeriesRepoBranches.T2, Both, [Id('a')]);
        _branches.SubSeries("Client", SeriesRepoBranches.T2, Both, ["2026-10-12-0c0ca"], seriesBase: "2026-10-12-0c0c");

        var result = await _branches.ReadAsync("Server", Both);

        result.State.Should().Be(SpecSetBranchState.Unreadable);
        result.Why.Should().Contain("different bases");
    }

    [Fact]
    public async Task SeriesReader_BrokenManifestInAnotherRunRepo_IsUnreadable()
    {
        _branches.SubSeries("Server", SeriesRepoBranches.T2, ["Server"], [Id('a')]);
        _branches.Repo("Client").Seed($".agentsmith/series/{SeriesRepoBranches.Base}.yaml",
            $"ticket: {SeriesRepoBranches.Key}\nspecs: [this is not\n  a document");

        var result = await _branches.ReadAsync("Server", Both);

        result.State.Should().Be(SpecSetBranchState.Unreadable);
        result.Why.Should().StartWith("Client: ");
    }

    [Fact]
    public async Task SeriesReader_NothingInAnyRunRepo_IsNothingAtThePath()
    {
        _branches.Repo("Server");
        _branches.Repo("Client");

        var result = await _branches.ReadAsync("Server", Both);

        result.State.Should().Be(SpecSetBranchState.NothingAtThePath);
    }

    [Fact]
    public async Task SeriesReader_OneRepoSeries_ReadsInManifestOrderAsBefore()
    {
        _branches.SubSeries("Server", SeriesRepoBranches.T2, null, [Id('b'), Id('a')]);

        var result = await _branches.ReadAsync("Server", Both);

        result.Set!.Phases.Select(p => p.PhaseId).Should().Equal([Id('b'), Id('a')],
            "a single sub-series keeps its manifest's order, as it always read");
        result.Read!.LastCommitSha.Should().Be("sha-Server");
    }

    private static string Id(char letter) => SeriesRepoBranches.Id(letter);
}
