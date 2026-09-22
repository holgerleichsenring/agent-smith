using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-17-0e79b: the record of a filed ticket is re-openable by the same spec key the run
/// resolves, and approving again upserts it in place rather than duplicating it.
/// <para>
/// 2026-09-22-6ad7: the load still has no caller — nothing in the design conversation re-opens a
/// record — and the run-side merge that policed a re-approval against the branch is gone with the
/// record as a source. The append-only rule it encoded belongs to whoever publishes a
/// re-approval onto a branch that already carries an executed head.
/// </para>
/// </summary>
public sealed class ApprovedSetAmendmentTests
{
    [Fact]
    public async Task AmendmentEntry_LoadsTheRecordByTheTicketsSpecKey()
    {
        var store = ApprovedSetDoubles.Store();
        var recorder = Recorder(store);
        await recorder.RecordAsync(State(), Project(), "19106", [Draft("p19106a")], default);

        var record = await recorder.LoadAsync(Project(), "19106", default);

        record.Should().NotBeNull("the conversation edits the record, never the branch");
        record!.Key.Should().Be(SpecSetKey.For("azuredevops", "19106").Value);
        record.Set.Phases.Should().ContainSingle().Which.PhaseId.Should().Be("p19106a");
    }

    [Fact]
    public async Task AmendmentEntry_ATicketNobodyApproved_LoadsNothing()
    {
        (await Recorder(ApprovedSetDoubles.Store()).LoadAsync(Project(), "19999", default))
            .Should().BeNull();
    }

    [Fact]
    public async Task Reapproval_WritesARecordWithANewerApproval()
    {
        var store = ApprovedSetDoubles.Store();
        var clock = new SteppingClock(ApprovedSets.Noon);
        var recorder = Recorder(store, clock);
        await recorder.RecordAsync(State(), Project(), "19106", [Draft("p19106a")], default);
        var first = (await recorder.LoadAsync(Project(), "19106", default))!.Approval!;

        await recorder.RecordAsync(
            State(), Project(), "19106", [Draft("p19106a"), Draft("p19106b")], default);

        var again = (await recorder.LoadAsync(Project(), "19106", default))!;
        again.Approval!.IsNewerThan(first).Should().BeTrue(
            "the newer instant is what the run compares against the branch's own");
        again.Set.Phases.Should().HaveCount(2, "the record is upserted in place, never duplicated");
    }

    private static ApprovedPhaseSetRecorder Recorder(
        ISpecApprovalStore store, TimeProvider? time = null) =>
        new(store, time ?? TimeProvider.System, NullLogger<ApprovedPhaseSetRecorder>.Instance);

    private static PhaseDraft Draft(string id) =>
        new(id, $"Do {id}", $"phase: {id}\ngoal: \"Do {id}\"", []) { Done = ["It is done."] };

    private static ConversationState State() => new()
    {
        JobId = "session-1",
        ChannelId = "channel",
        UserId = "sample.user",
        Platform = "dashboard",
        Project = "sample",
        TicketId = string.Empty,
        StartedAt = ApprovedSets.Noon,
        Scope = new ActiveScope { Project = "sample", Repos = ["sample-api"] },
    };

    private static ResolvedProject Project() => new()
    {
        Name = "sample",
        Tracker = new TrackerConnection { Type = TrackerType.AzureDevOps },
        Repos = [new RepoConnection { Name = "sample-api" }],
    };

    private sealed class SteppingClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now = _now.AddMinutes(1);
    }
}
