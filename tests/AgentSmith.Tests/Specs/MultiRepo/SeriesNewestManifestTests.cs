using AgentSmith.Application.Services.Specs;
using FluentAssertions;

namespace AgentSmith.Tests.Specs.MultiRepo;

/// <summary>2026-10-12-f5f7b: which sub-series' manifest speaks for the set.</summary>
public sealed class SeriesNewestManifestTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Pick_TieWithDifferentMembers_IsRefused()
    {
        var (newest, why) = new SeriesNewestManifest().Pick(
            [Read("Server", At, "Server", "Client"), Read("Client", At, "Client")], "Server");

        newest.Should().BeNull();
        why.Should().Contain("share the newest revision time");
    }

    [Fact]
    public void Pick_TieWithSameMembers_PrefersTheCarryingRepository()
    {
        var (newest, why) = new SeriesNewestManifest().Pick(
            [Read("Server", At, "Server", "Client"), Read("Client", At, "Client", "Server")], "Client");

        why.Should().BeNull();
        newest!.Repo.Should().Be("Client");
    }

    [Fact]
    public void Pick_NoManifestCarriesAParsedTime_IsRefused()
    {
        var (newest, why) = new SeriesNewestManifest().Pick(
            [Read("Server", null, "Server"), Read("Client", null, "Client")], "Server");

        newest.Should().BeNull();
        why.Should().Contain("no readable revision time");
    }

    [Fact]
    public void Pick_LegacyManifestsAndNoneInTheCarryingRepository_IsRefused()
    {
        var (newest, why) = new SeriesNewestManifest().Pick(
            [Read("Server", At), Read("Client", At)], "Worker");

        newest.Should().BeNull();
        why.Should().Contain("'Worker'");
    }

    private static SubSeriesRead Read(string repo, DateTimeOffset? stamp, params string[] members) =>
        new(repo, SubSeriesState.Found)
        {
            Base = "2026-10-12-0b0b", Stamp = stamp,
            Document = new SeriesManifestDocument { SeriesRepos = members.Length == 0 ? null : [.. members] },
        };
}
