using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using static AgentSmith.Tests.SpecDialog.TicketGraph.TicketGraphRig;

namespace AgentSmith.Tests.SpecDialog.TicketGraph;

/// <summary>
/// 2026-10-14-dbcem: every stored requires edge of a filed list is shown on the tracker once, after the starts, under
/// one deadline; what the tracker refuses or never answers is a filing note, and the filing succeeds regardless.
/// </summary>
public sealed class TicketGraphLinksTests
{
    private readonly TicketGraphRig _rig = new();

    [Fact]
    public async Task TicketGraphLinks_TrackerHangs_StopsAtTheDeadlineAndNamesTheRest()
    {
        var deadline = new ManualDeadline();
        _rig.Time = deadline;
        // The second call hangs and ignores its token, as a client stuck in its own timeout would.
        _rig.Links.Answer = (i, _) =>
        {
            if (i == 0) return Task.FromResult(RequiresLinkResult.Done);
            deadline.Fire();
            return new TaskCompletionSource<RequiresLinkResult>().Task;
        };

        var notes = await _rig.LinkPass().LinkAsync(_rig.Project, [Edge("102", "101"), Edge("103", "102"), Edge("104", "101")]);

        deadline.DueTime.Should().Be(TimeSpan.FromSeconds(60)).And.Be(TicketGraphLinks.Deadline);
        _rig.Links.Calls.Should().HaveCount(2, "the pass stops at the call the deadline cut");
        notes.Should().Equal("Not linked on the tracker (the tracker did not answer): #103 requires #102, #104 requires #101; "
            + "the requirements are stored and hold those tickets anyway.");
    }

    [Fact]
    public async Task TicketGraphLinks_FirstUnreached_StopsAndNamesEveryUnreachedEdgeInOneNote()
    {
        _rig.Links.Answer = (i, _) => Task.FromResult(i == 1 ? RequiresLinkResult.Unreached("the tracker did not answer") : RequiresLinkResult.Done);

        var notes = await _rig.LinkPass().LinkAsync(_rig.Project, [Edge("102", "101"), Edge("103", "102"), Edge("104", "103")]);

        _rig.Links.Calls.Should().Equal([("102", "101"), ("103", "102")]);
        notes.Should().ContainSingle().Which.Should().Be("Not linked on the tracker (the tracker did not answer): "
            + "#103 requires #102, #104 requires #103; the requirements are stored and hold those tickets anyway.");
    }

    [Fact]
    public async Task TicketGraphLinks_IncompleteTicket_ItsStoredEdgesAreLinked()
    {
        List<FiledTicket> filed = [new("https://tracker.test/102", "t") { TicketId = "102" }];

        await _rig.Starts().SettleAsync(_rig.Tracker, _rig.Project,
            [new CreatedGraphTicket(new CreatedTicket(new TicketId("102"), null), [Edge("102", "101")], Complete: false)],
            ["stamp"], true, filed);

        _rig.Links.Calls.Should().Equal([("102", "101")]);
    }

    [Theory]
    [InlineData(TrackerType.Jira, "PROJ-102", "PROJ-101")]
    [InlineData(TrackerType.GitHub, "#102", "#101")]
    public async Task TicketGraphLinks_Refused_OneNoteNamingBothTicketsByTrackerKey(TrackerType type, string dependent, string required)
    {
        var rig = new TicketGraphRig(type);
        rig.Links.Answer = (_, _) => Task.FromResult(RequiresLinkResult.Refused("not authorized"));

        var report = await rig.FileAsync(List(Ticket("api"), Ticket("ui", "api")));

        report.Error.Should().BeNull("a refused link leaves the filing successful");
        report.Notes.Should().ContainSingle().Which.Should().Be($"The tracker does not show that {dependent} requires {required} "
            + $"(not authorized); the requirement is stored and holds {dependent} anyway.");
    }

    [Fact]
    public async Task TicketGraphLinks_FactoryWithoutRequiresLink_FilingSucceedsWithOneNotePerEdge()
    {
        var pass = new TicketGraphLinks(new StubTicketProviderFactory(), TimeProvider.System, NullLogger<TicketGraphLinks>.Instance);

        var notes = await pass.LinkAsync(_rig.Project, [Edge("102", "101"), Edge("103", "101")]);

        notes.Should().HaveCount(2).And.AllSatisfy(n => n.Should().Contain("(StubTicketProviderFactory builds no tracker link)"));
    }

    [Fact]
    public async Task TicketGraphLinks_CallerCancelled_StillLinksEveryStoredEdge()
    {
        using var cancel = new CancellationTokenSource();
        _rig.Tracker.AfterCreate = count => { if (count == 2) cancel.Cancel(); };
        var tokens = new List<bool>();
        _rig.Links.Answer = (_, ct) => { tokens.Add(ct.IsCancellationRequested); return Task.FromResult(RequiresLinkResult.Done); };

        var report = await _rig.FileAsync(List(Ticket("api"), Ticket("ui", "api"), Ticket("docs", "ui")), ct: cancel.Token);

        report.Error.Should().Contain("interrupted");
        _rig.Links.Calls.Should().Equal([("102", "101")]);
        tokens.Should().Equal([false], "only the deadline cancels the pass");
    }

    [Fact]
    public async Task TicketGraphFiler_PartialFailure_LinksTheCreatedPrefix()
    {
        _rig.Tracker.FailCreateAt = 2;

        var report = await _rig.FileAsync(List(Ticket("api"), Ticket("ui", "api"), Ticket("docs", "ui")));

        report.Error.Should().NotBeNull();
        _rig.Links.Calls.Should().Equal([("102", "101")]);
    }

    [Fact]
    public async Task TicketGraphFiler_Links_AfterTheStartPass()
    {
        var startedWhenLinked = new List<int>();
        _rig.Links.OnCall = () => startedWhenLinked.Add(_rig.Tracker.Moves.Count);

        await _rig.FileAsync(List(Ticket("api"), Ticket("ui", "api")));

        startedWhenLinked.Should().Equal([1], "the root was started before anything was linked");
    }

    private TicketRequirementEdge Edge(string ticket, string required) => new(
        "sample-tracker", _rig.KeyOf(ticket), ticket, "proj", _rig.KeyOf(required), required, true, DateTimeOffset.UnixEpoch, ConversationId);

    /// <summary>A clock whose one timer — the pass's deadline — fires when the test says so.</summary>
    private sealed class ManualDeadline : TimeProvider
    {
        private TimerCallback? _callback;
        private object? _state;

        public TimeSpan DueTime { get; private set; }

        public void Fire() => _callback?.Invoke(_state);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            (_callback, _state, DueTime) = (callback, state, dueTime);
            return new NoTimer();
        }

        private sealed class NoTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
