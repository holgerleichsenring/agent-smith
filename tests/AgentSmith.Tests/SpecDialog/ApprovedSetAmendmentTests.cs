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
/// 2026-09-17-0e79b: a change to an approved set is made by approving it AGAIN. The conversation
/// re-opens the record of the ticket it already filed, by the same spec key the run resolves, and
/// approving writes a record with a newer approval over it — which is what beats the branch.
/// <para>
/// Which phases already ran lives on the branch and the dialog has no clone of it, so the
/// constraint is enforced by the RUN: the positional merge keeps the executed head exactly as it
/// ran and names what a re-approval would have changed or dropped.
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

    /// <summary>
    /// The merge is POSITIONAL, so a re-approval that reordered an early phase would silently
    /// change which spec an executed position claims to hold. The reorder does NOT take effect:
    /// the head is kept exactly as it ran and the run names what it discarded. This is the case
    /// that still has work AFTER the head — the edit-only case is refused outright above.
    /// </summary>
    [Fact]
    public void AmendmentEntry_ReorderingAPositionBeforeTheHead_KeepsTheHeadAndNamesTheDiscard()
    {
        var branch = Branch(["p19106a", "p19106b", "p19106c"], executed: ["p19106a", "p19106b"]);
        var reordered = ApprovedSets.Set(
            "azdo-19106",
            [ApprovedSets.Phase("p19106b"), ApprovedSets.Phase("p19106a"), ApprovedSets.Phase("p19106c")],
            ApprovedSets.Approval(ApprovedSets.Noon.AddHours(1)));

        var merged = ApprovedSetMerge.Over(reordered, branch);

        merged.Set!.Phases.Select(p => p.PhaseId).Should().Equal("p19106a", "p19106b", "p19106c");
        merged.Note.Should().Contain("p19106a").And.Contain("p19106b")
            .And.Contain("already ran", "the run says which positions it would not let move");
    }

    /// <summary>
    /// The edit-only case is REFUSED outright, not kept-and-named: a re-approval that changes an
    /// executed phase and adds nothing after it would publish, find nothing left to run and
    /// report success having discarded every change the operator made.
    /// </summary>
    [Fact]
    public void AmendmentEntry_EditingAnExecutedPhaseAndAddingNothing_IsRefused()
    {
        var branch = Branch(["p19106a", "p19106b"], executed: ["p19106a", "p19106b"]);
        var editedOnly = ApprovedSets.Set(
            "azdo-19106",
            [ApprovedSets.Phase("p19106a", "Rewritten after it ran"), ApprovedSets.Phase("p19106b")],
            ApprovedSets.Approval(ApprovedSets.Noon.AddHours(1)));

        var merged = ApprovedSetMerge.Over(editedOnly, branch);

        merged.Set.Should().BeNull("publishing it would report success with nothing to do");
        merged.Error.Should().Contain("p19106a").And.Contain("NEW phase");
    }

    [Fact]
    public void AmendmentEntry_ShorterThanTheExecutedHead_NamesWhatItWouldDrop()
    {
        var branch = Branch(["p19106a", "p19106b"], executed: ["p19106a", "p19106b"]);
        var shorter = ApprovedSets.Set(
            "azdo-19106", [ApprovedSets.Phase("p19106a")],
            ApprovedSets.Approval(ApprovedSets.Noon.AddHours(1)));

        var merged = ApprovedSetMerge.Over(shorter, branch);

        merged.Set.Should().BeNull();
        merged.Error.Should().Contain("p19106b");
    }

    private static ApprovedPhaseSetRecorder Recorder(
        ISpecApprovalStore store, TimeProvider? time = null) =>
        new(store, time ?? TimeProvider.System, NullLogger<ApprovedPhaseSetRecorder>.Instance);

    private static SpecSet Branch(IReadOnlyList<string> phases, IReadOnlyList<string> executed) =>
        new("azdo-19106",
            [.. phases.Select(id => ApprovedSets.Phase(id))],
            SpecAccounting.Empty,
            [new SpecRevision(1, SpecRevisionCause.Initial, ApprovedSets.Noon)],
            SpecSource.BranchArtifact,
            ExecutedPhaseIds: executed,
            Approval: ApprovedSets.Approval(ApprovedSets.Noon));

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
