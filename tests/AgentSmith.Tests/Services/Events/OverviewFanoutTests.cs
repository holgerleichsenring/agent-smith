using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Services.Events;
using AgentSmith.Tests.Server.Auth;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Events;

/// <summary>
/// 2026-10-15-4a4dc: an overview nudge reaches the overview group — where global readers sit — and the
/// group of the project it is about, where that project's readers sit. The installation's rollup stays on
/// the overview group alone, and with enforcement off nothing new is read or sent.
/// </summary>
public sealed class OverviewFanoutTests : IDisposable
{
    private static readonly string Alpha = HubGroups.OverviewOf("alpha");

    private readonly HubProjectStore _store = new();
    private readonly RecordingHubContext _hub = new();

    public void Dispose() => _store.Dispose();

    [Fact]
    public async Task OverviewAudience_EnforceOff_IsTheOverviewGroupAloneAndReadsNothing()
    {
        await _store.SeedAsync(db => ProjectScopeRows.RunAsync(db, "r-off", "alpha"));
        var audience = Audience(enforce: false);

        (await audience.OfRunAsync("r-off", null, CancellationToken.None)).Should().Equal(HubGroups.Overview);
        (await audience.OfLiveRunAsync("r-off", null, CancellationToken.None)).Should().Equal(HubGroups.Overview);
        audience.OfProject("alpha").Should().Equal(HubGroups.Overview);
        audience.Everyone().Should().Equal(HubGroups.Overview);
        _store.Reads.Should().Be(0);
    }

    [Fact]
    public async Task JobsHubFanout_RunsChanged_GoesToTheOverviewGroupAndTheRunsProjectGroup()
    {
        await Fanout().ToOverviewAsync(RunSnapshot.Empty("r-1") with { Project = "alpha" }, CancellationToken.None);

        _hub.Messages.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Groups = new[] { HubGroups.Overview, Alpha }, Method = "RunsChanged", Arguments = new object[] { "r-1" } });
        _store.Reads.Should().Be(0, "the fold already carried the project");
    }

    [Fact]
    public async Task JobsHubFanout_RunsChangedForAProjectlessRun_GoesToTheOverviewGroupOnly()
    {
        await Fanout().ToOverviewAsync(RunSnapshot.Empty("r-cli"), CancellationToken.None);

        _hub.Messages.Should().ContainSingle().Which.Groups.Should().Equal(HubGroups.Overview);
    }

    [Fact]
    public async Task JobsHubFanout_FinishedSnapshot_ForgetsTheRun()
    {
        await _store.SeedAsync(db => ProjectScopeRows.RunAsync(db, "r-mid", "alpha"));
        var index = _store.Index();
        var fanout = Fanout(index);

        await fanout.ToOverviewAsync(RunSnapshot.Empty("r-mid"), CancellationToken.None);
        await fanout.ToOverviewAsync(RunSnapshot.Empty("r-mid") with { FinishedAt = DateTimeOffset.UtcNow }, CancellationToken.None);
        await index.OfAsync("r-mid", null, CancellationToken.None);

        _hub.Messages.Should().HaveCount(2).And.OnlyContain(sent => sent.Groups.Contains(Alpha));
        _store.Reads.Should().Be(2, "the live run was remembered once and forgotten when it finished");
    }

    [Fact]
    public async Task JobsHubFanout_TicketActivityChanged_GoesToTheOverviewGroupAndTheTicketsProjectGroup()
    {
        await Fanout().ToTicketActivityAsync(
            new TicketActivityChangedEvent("tracker", "alpha", "github:7", DateTimeOffset.UtcNow), CancellationToken.None);

        _hub.Messages.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Groups = new[] { HubGroups.Overview, Alpha }, Method = "TicketActivityChanged",
            Arguments = new object[] { "alpha", "github:7" },
        });
    }

    [Fact]
    public async Task JobsHubFanout_SystemActivityUpdated_GoesToTheOverviewGroupOnly()
    {
        await Fanout().ToSystemActivityAsync(SystemActivitySnapshot.Empty, CancellationToken.None);

        _hub.Messages.Should().ContainSingle().Which.Groups.Should().Equal(HubGroups.Overview);
    }

    [Fact]
    public async Task RunListNudge_RunsChanged_ReachesTheRunsProjectGroup()
    {
        await _store.SeedAsync(db => ProjectScopeRows.RunAsync(db, "r-queued", "alpha"));

        await new JobsHubRunListNudge(_hub.Hub, Audience()).RunsChangedAsync("r-queued", CancellationToken.None);

        _hub.Messages.Should().ContainSingle().Which.Groups.Should().Equal(HubGroups.Overview, Alpha);
    }

    private JobsHubFanout Fanout(RunProjectIndex? index = null)
    {
        index ??= _store.Index();
        return new JobsHubFanout(_hub.Hub, Audience(index: index), index);
    }

    internal OverviewAudience Audience(bool enforce = true, RunProjectIndex? index = null) => new(
        new TokenAuthorityConfig { Enforce = enforce }, index ?? _store.Index(), ProjectScopeUnderTest.Projects("alpha", "beta"));
}
