using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.MultiRepo;

/// <summary>2026-10-12-f5f7b: the join's order checks and the manifest's revision time.</summary>
public sealed class SeriesJoinUnitTests
{
    [Fact]
    public void Of_SameSpecInTwoRepos_IsRefused()
    {
        var (_, _, why) = new SeriesJoinOrder().Of([Read("Server", "x1a"), Read("Client", "x1a")]);

        why.Should().Contain("x1a").And.Contain("more than one repository");
    }

    [Fact]
    public void Of_RequiresCycle_IsRefused()
    {
        var (_, _, why) = new SeriesJoinOrder().Of(
            [Read("Server", "x1a", requires: "x1b"), Read("Client", "x1b", requires: "x1a")]);

        why.Should().Contain("requires");
    }

    [Fact]
    public void Join_UnreadableRepoAmongSeveral_NamesTheRepository()
    {
        var joined = new SeriesJoin(new SeriesNewestManifest(), new SeriesJoinOrder()).Join(
            [Read("Server", "x1a"), SubSeriesRead.Failed("Client", "it broke")], "Server");

        joined.Otherwise!.Why.Should().Be("Client: it broke");
    }

    [Fact]
    public void Join_CarryingRepoNotCheckedOutAndNothingElse_IsUnreadable()
    {
        var joined = new SeriesJoin(new SeriesNewestManifest(), new SeriesJoinOrder()).Join(
            [new SubSeriesRead("Server", SubSeriesState.NotCheckedOut), new SubSeriesRead("Client", SubSeriesState.Nothing)],
            "Server");

        joined.Otherwise!.State.Should().Be(SpecSetBranchState.Unreadable);
        joined.Otherwise.Why.Should().Contain("'Server'").And.Contain("not checked out");
    }

    [Fact]
    public void CurrentRevisionTimeOf_ParsesTheLastRevisionAndMarksTheRestUnparsed()
    {
        var manifest = new SeriesManifest();
        var doc = manifest.Parse("ticket: k\nrevisions:\n- number: 1\n  at: 'garbage'\n- number: 2\n  at: '2026-10-12T10:00:00+00:00'\nspecs: []\n")!;

        manifest.CurrentRevisionTimeOf(doc).Should().Be(new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero));
        manifest.CurrentRevisionTimeOf(manifest.Parse("ticket: k\nrevisions:\n- number: 1\n  at: 'garbage'\n")!)
            .Should().BeNull();
        manifest.CurrentRevisionTimeOf(manifest.Parse("ticket: k\n")!).Should().BeNull();
    }

    [Fact]
    public void Parse_SeriesRepos_ReadsTheMembersAndSerializeOmitsThemWhenUnset()
    {
        var manifest = new SeriesManifest();

        manifest.Parse("ticket: k\nseries_repos: [Server, Client]\n")!.SeriesRepos.Should().Equal("Server", "Client");
        manifest.Serialize(new SpecSet("k", [], SpecAccounting.Empty,
                [new SpecRevision(1, SpecRevisionCause.Initial, DateTimeOffset.UnixEpoch)], SpecSource.Derived, Series: "b"))
            .Should().NotContain("series_repos", "today's writes stay byte-for-byte what they were");
    }

    private static SubSeriesRead Read(string repo, string id, string? requires = null) =>
        new(repo, SubSeriesState.Found)
        {
            Base = "x1",
            Specs = [new SpecPhase(new PhaseDraft(id, "g", $"spec: {id}", requires is null ? [] : [requires]), id, string.Empty, [])],
        };
}
