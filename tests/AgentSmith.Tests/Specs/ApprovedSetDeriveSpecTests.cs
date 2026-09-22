using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-17-0e79a: DeriveSpec end to end over an approved record — the first run publishes the
/// approved set to the ticket branch with a pointer and a draft pull request, and set.yaml records
/// the approval it was published from.
/// <para>
/// 2026-09-22-6ad7: that publish is now the HAND-OFF, and it fires only where the branch carries
/// nothing at the path. A filed ticket whose branch carries no readable set PARKS — it does not
/// derive a guess, and it is not finalized into a failure status either.
/// </para>
/// </summary>
public sealed class ApprovedSetDeriveSpecTests
{
    private const string Key = "azdo-19106";

    [Fact]
    public async Task ApprovedSet_FirstRun_IsCommittedToTheTicketBranchAndPointed()
    {
        var harness = Harness();
        await harness.Approvals.SaveAsync(
            ApprovedSets.Record(Key, ApprovedSets.Noon, ["p19106a", "p19106b"]), default);

        var result = await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        result.IsSuccess.Should().BeTrue();
        harness.Deriver.Calls.Should().Be(0, "an approved set is worked, never re-derived");
        harness.Writer.Written!.Phases.Select(p => p.PhaseId).Should().Equal("p19106a", "p19106b");
        harness.Writer.Written.Source.Should().Be(SpecSource.Approved);
        harness.PullRequests.Opened.Should().Be(1, "the reviewer needs the draft pull request at the spec commit");
        (await harness.Pointers.GetAsync(string.Empty, Key, default))!.RevisionSha
            .Should().Be("revision-sha");
    }

    [Fact]
    public async Task ApprovedSet_FirstRun_SetYamlRecordsTheApprovalItWasPublishedFrom()
    {
        var harness = Harness();
        await harness.Approvals.SaveAsync(
            ApprovedSets.Record(Key, ApprovedSets.Noon, conversation: "session-77"), default);

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        var index = new SpecSetIndex();
        var written = index.ApprovalOf(index.Parse(index.Serialize(harness.Writer.Written!))!);
        written!.At.Should().Be(ApprovedSets.Noon);
        written.Conversation.Should().Be("session-77",
            "the next run compares a fresh record against exactly this instant");
    }

    [Fact]
    public async Task ApprovedSet_FirstRun_RevisionCauseNamesTheApproval()
    {
        var harness = Harness();
        await harness.Approvals.SaveAsync(
            ApprovedSets.Record(Key, ApprovedSets.Noon, conversation: "session-77"), default);

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        var revision = harness.Writer.Written!.Current;
        revision.Number.Should().Be(1);
        revision.Cause.Should().Be($"{SpecRevisionCause.Approval} session-77");
    }

    [Fact]
    public async Task ApprovedSet_WriteFailed_RunContinuesAndTheNextRunIsHandedItAgain()
    {
        var harness = Harness();
        harness.Writer.Fail = true;
        await harness.Approvals.SaveAsync(ApprovedSets.Record(Key, ApprovedSets.Noon), default);

        var result = await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        result.IsSuccess.Should().BeTrue("losing the reviewer's view must not lose the run");
        (await harness.Approvals.GetAsync(ApprovedSets.Tracker, Key, default)).Should().NotBeNull(
            "the record is never consumed — a re-trigger before the first publish gets it again");
    }

    [Fact]
    public async Task ApprovedSet_WithAFencedTicketDescription_WinsOverTheDescription()
    {
        var harness = Harness();
        await harness.Approvals.SaveAsync(
            ApprovedSets.Record(Key, ApprovedSets.Noon, ["p19106a"]), default);

        await harness.Handler().ExecuteAsync(harness.Context(Ticket(EmbeddedSpec)), default);

        harness.Writer.Written!.Source.Should().Be(SpecSource.Approved);
        harness.Writer.Written.Phases.Should().ContainSingle().Which.PhaseId.Should().Be("p19106a",
            "a description anyone can edit does not outrank the set a person approved");
    }

    [Fact]
    public async Task DeriveSpec_AFiledTicketWithNoSetOnTheBranch_DerivesNothing()
    {
        var harness = Harness();

        await harness.Handler().ExecuteAsync(harness.Context(Filed()), default);

        harness.Deriver.Calls.Should().Be(0,
            "deriving here would silently replace a ratified spec with a guess");
        harness.Writer.Written.Should().BeNull("there is nothing to publish");
    }

    /// <summary>
    /// 2026-09-22-6ad7: a failed step finalizes the ticket into the failure status, taking it out
    /// of the open set because a file is not on a branch yet. The park leaves it where a person
    /// can put the specs there.
    /// </summary>
    [Fact]
    public async Task DeriveSpec_AFiledTicketWithNoSetOnTheBranch_HandsBackInsteadOfFailingTheStep()
    {
        var harness = Harness();
        var context = harness.Context(Filed());

        var result = await harness.Handler().ExecuteAsync(context, default);

        result.IsSuccess.Should().BeTrue("a failed step would close the ticket as failed");
        context.Pipeline.Get<SpecHandback>(ContextKeys.SpecHandback).Case
            .Should().Be(SpecHandbackCase.SpecificationMissingFromBranch);
    }

    /// <summary>
    /// The branch holds something this run cannot read. The record is NOT stood in front of it —
    /// that is how an operator's broken edit disappears — and the park says an approval exists.
    /// </summary>
    [Fact]
    public async Task DeriveSpec_AFiledTicketWhoseApprovalExists_SaysItsSpecsNeverReachedTheBranch()
    {
        var harness = Harness();
        harness.Branch.Seed($".agentsmith/specs/{Key}/set.yaml", "key: [this is not\n  a document");
        await harness.Approvals.SaveAsync(
            ApprovedSets.Record(Key, ApprovedSets.Noon, conversation: "session-77"), default);
        var context = harness.Context(Filed());

        await harness.Handler().ExecuteAsync(context, default);

        harness.Writer.Written.Should().BeNull("a copy does not stand in for an unreadable set");
        var reason = context.Pipeline.Get<SpecHandback>(ContextKeys.SpecHandback).Reason;
        reason.Should().Contain("An approval for this ticket exists").And.Contain("session-77");
    }

    [Fact]
    public async Task DeriveSpec_AFiledTicketNobodyApproved_SaysThatInstead()
    {
        var harness = Harness();
        var context = harness.Context(Filed());

        await harness.Handler().ExecuteAsync(context, default);

        context.Pipeline.Get<SpecHandback>(ContextKeys.SpecHandback).Reason
            .Should().Contain("nobody approved it")
            .And.NotContain("An approval for this ticket exists");
    }

    [Fact]
    public async Task DeriveSpec_TheHandbackReason_NamesTheBranchPathTheSpecsBelongAt()
    {
        var harness = Harness();
        var context = harness.Context(Filed());

        await harness.Handler().ExecuteAsync(context, default);

        context.Pipeline.Get<SpecHandback>(ContextKeys.SpecHandback).Reason
            .Should().Contain($"`{SpecSetKey.Root}/{Key}/`")
            .And.Contain(SpecSetKey.Root, "the operator is told where to put them");
    }

    /// <summary>
    /// 2026-09-17-0e79a review: the fence this phase removed must not come back as a PASTE.
    /// Anyone with tracker write access can put a ```yaml block in a filed ticket's description;
    /// on a stamped ticket the description is not a source, and the run parks.
    /// </summary>
    [Fact]
    public async Task ApprovedSet_FiledTicketWhoseDescriptionWasPastedInto_StillParks()
    {
        var harness = Harness();
        var context = harness.Context(Filed(EmbeddedSpec));

        await harness.Handler().ExecuteAsync(context, default);

        harness.Writer.Written.Should().BeNull("nothing is published from a pasted spec");
        harness.Deriver.Calls.Should().Be(0);
        context.Pipeline.Get<SpecHandback>(ContextKeys.SpecHandback).Case
            .Should().Be(SpecHandbackCase.SpecificationMissingFromBranch);
    }

    /// <summary>
    /// A HAND-WRITTEN phase ticket carries the phase label and no filing stamp: the framework did
    /// not file it, its spec legitimately lives in its description, and the p0315d path stands.
    /// </summary>
    [Fact]
    public async Task ApprovedSet_HandWrittenPhaseTicketWithNoRecord_StillUsesItsDescription()
    {
        var harness = Harness();

        var result = await harness.Handler().ExecuteAsync(
            harness.Context(Ticket(EmbeddedSpec, labels: ["phase"])), default);

        result.IsSuccess.Should().BeTrue();
        harness.Writer.Written!.Source.Should().Be(SpecSource.TicketDescription);
        harness.Writer.Written.Phases.Should().ContainSingle().Which.PhaseId.Should().Be("p9999");
    }

    [Fact]
    public async Task LoudMiss_PhaseTicketWithAParentStamp_StillDerives()
    {
        var harness = Harness();
        harness.Deriver.Result = Derivation();

        var result = await harness.Handler().ExecuteAsync(
            harness.Context(Ticket(labels:
                ["phase", FiledTicketLabels.ApprovedSetStamp, "phase-parent:42"])), default);

        result.IsSuccess.Should().BeTrue();
        harness.Deriver.Calls.Should().Be(1,
            "an epic child of the N-children shape deliberately carries no spec");
    }

    /// <summary>
    /// 2026-09-17-0e79a review: a failed step names the phase, not the reason. The same sentence
    /// is published on the run's gate trail, which is where a person reads why a run stopped.
    /// </summary>
    [Fact]
    public async Task LoudMiss_IsPublishedOnTheRunsGateTrail()
    {
        var events = new RecordingEventPublisher();
        var gate = new SpecCutGate(events, NullLogger<SpecCutGate>.Instance);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");

        var result = await gate.RefuseSpecAsync(pipeline, "19106", "the approval never arrived", default);

        result.IsSuccess.Should().BeFalse();
        events.Gates.Should().ContainSingle()
            .Which.Should().Match<GateCheckedEvent>(
                e => e.Gate == "spec-source" && !e.Passed && e.Reason.Contains("never arrived"));
    }

    private sealed class RecordingEventPublisher : IEventPublisher
    {
        internal List<GateCheckedEvent> Gates { get; } = [];

        public Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken)
        {
            if (runEvent is GateCheckedEvent gate) Gates.Add(gate);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 2026-09-17-0e79a review: the carried record belongs to another ticket, so the run has no
    /// record — and a FILED ticket parks instead of working to someone else's specification.
    /// </summary>
    [Fact]
    public async Task ApprovedSet_CarriedRecordForAnotherTicket_Parks()
    {
        var harness = Harness();
        var foreign = SpecApprovalJson.Write(ApprovedSets.Record("azdo-999", ApprovedSets.Noon));
        var context = harness.Context(Filed(), foreign);

        await harness.Handler().ExecuteAsync(context, default);

        harness.Deriver.Calls.Should().Be(0);
        context.Pipeline.Get<SpecHandback>(ContextKeys.SpecHandback).Reason
            .Should().Contain("nobody approved it");
    }

    [Fact]
    public async Task ApprovedSet_NoRecordAndNoPhaseLabel_DerivesExactlyAsBefore()
    {
        var harness = Harness();
        harness.Deriver.Result = Derivation();

        var result = await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        result.IsSuccess.Should().BeTrue();
        harness.Deriver.Calls.Should().Be(1);
        harness.Writer.Written!.Source.Should().Be(SpecSource.Derived);
    }

    [Fact]
    public async Task ApprovedSet_MalformedEmbeddedSpecAndNoRecord_StillFailsLoudly()
    {
        var harness = Harness();

        var result = await harness.Handler().ExecuteAsync(
            harness.Context(Ticket("```yaml\nphase: nope\ngoal: 3\n```")), default);

        result.IsSuccess.Should().BeFalse("shipping a spec and getting it wrong is not 'derive one'");
        harness.Deriver.Calls.Should().Be(0);
    }

    /// <summary>
    /// The CARRY is the route every launcher has: a chat run is a container and a CLI run is a
    /// process, and neither composition swaps a store in.
    /// </summary>
    [Fact]
    public async Task ApprovedSet_CarriedIntoAProcessWithNoStore_IsStillTheSource()
    {
        var harness = Harness();
        var carried = SpecApprovalJson.Write(ApprovedSets.Record(Key, ApprovedSets.Noon, ["p19106a"]));

        await harness.Handler().ExecuteAsync(harness.Context(Ticket(), carried), default);

        harness.Writer.Written!.Source.Should().Be(SpecSource.Approved);
        harness.Deriver.Calls.Should().Be(0);
    }

    /// <summary>
    /// 2026-09-22-6ad7: the branch ANSWERED, so it is the set — whatever instant the record in
    /// the store carries. There is no comparison left to make.
    /// </summary>
    [Fact]
    public async Task ApprovedSet_ARecordInTheStore_NeverDisplacesASetTheBranchAnswered()
    {
        var harness = Harness();
        harness.PointerSha = "branch-sha";
        harness.Branch.SeedSet(BranchSetYaml, new Dictionary<string, string>
        {
            ["p19106a-onthebranch"] = "phase: p19106a\ngoal: \"As it stands on the branch\"\ndone:\n  - \"Done.\"",
        });
        await harness.Approvals.SaveAsync(
            ApprovedSets.Record(Key, ApprovedSets.Noon.AddHours(1), ["p19106z"]), default);

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Writer.Written!.Phases.Should().ContainSingle().Which.PhaseId.Should().Be("p19106a",
            "the branch answered, so the record is not consulted at all");
    }

    private const string BranchSetYaml = """
        key: azdo-19106
        source: Approved
        phases:
        - p19106a-onthebranch
        revisions:
        - number: 1
          cause: approved in design conversation session-1
          at: 2026-09-17T12:00:00.0000000+00:00
        approved_at: 2026-09-17T12:00:00.0000000+00:00
        approved_in_conversation: session-1
        approved_by: sample.approver
        """;

    private const string EmbeddedSpec = """
        Please add the widget endpoint.

        ```yaml
        phase: p9999
        goal: "Add a widget endpoint to the sample service"
        steps:
          - id: impl
            action: "Add the widget endpoint + handler"
        done:
          - "GET /widget returns the widget"
        ```
        """;

    private static ApprovedSetHarness Harness() => new() { Branch = { Key = Key } };

    /// <summary>A ticket the framework filed from an approved set: the label and the stamp.</summary>
    private static Ticket Filed(string description = "Migrate the client.") =>
        Ticket(description, ["phase", FiledTicketLabels.ApprovedSetStamp]);

    private static Ticket Ticket(string description = "Migrate the client.", IReadOnlyList<string>? labels = null) =>
        new(new TicketId("19106"), "Migrate the client", description, null, "open", "azdo", labels ?? []);

    private static SpecDerivation Derivation()
    {
        var segments = TicketSegmenter.Segment("Migrate the client.");
        var phase = new SpecPhase(
            ApprovedSets.Phase("p19106a").Draft, "p19106a", string.Empty, [.. segments.Select(s => s.Id)]);
        return new SpecDerivation(
            new SpecSet(
                Key, [phase], SpecAccountingBuilder.Build([phase], [], segments),
                [new SpecRevision(1, SpecRevisionCause.Initial, ApprovedSets.Noon)],
                SpecSource.Derived),
            []);
    }
}
