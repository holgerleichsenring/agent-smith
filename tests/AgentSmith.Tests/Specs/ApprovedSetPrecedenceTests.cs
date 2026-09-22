using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-22-6ad7: the branch is the only SET a run reads. A branch that ANSWERED is the set and
/// the record is not consulted; a branch with NOTHING AT THE PATH is handed the record's set once;
/// a branch that answered BADLY is not replaced by a copy at all.
/// <para>
/// 2026-09-17-0e79a's versioned arm is gone with the instants it compared: the approval a set was
/// published from still travels in <c>set.yaml</c>, which is what keeps an approved set from being
/// re-cut, and that is the only thing the comparison was ever read for.
/// </para>
/// </summary>
public sealed class ApprovedSetPrecedenceTests
{
    private const string Key = "azdo-19106";

    private readonly SpecSourceResolver _sut = new(
        new PhaseSpecFromTicket(
            new SpecDraftValidator(new PhaseSpecSchemaProvider()), new PhaseDraftReader()),
        new ApprovedSetHandoff(NullLogger<ApprovedSetHandoff>.Instance),
        new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance),
        NullLogger<SpecSourceResolver>.Instance);

    [Fact]
    public void SpecSource_ABranchSetAndARecordWithANewerApproval_WorksTheBranchSet()
    {
        var branch = Branch(ApprovedSets.Noon, ["p19106a"]);
        var record = ApprovedSets.Record(Key, ApprovedSets.Noon.AddHours(1), ["p19106x", "p19106y"]);

        var decision = _sut.Decide(branch, Filed(), null, new PipelineContext(), Key, record);

        decision.Source.Should().Be(SpecSource.BranchArtifact);
        decision.Set!.Phases.Select(p => p.PhaseId).Should().Equal(["p19106a"],
            "the branch answered, so the record is not consulted, merged or compared");
        decision.NeedsModel.Should().BeFalse("an approved set is read, never generated");
    }

    [Fact]
    public void SpecSource_ABranchSetThatDoesNotParse_IsNotReplacedByTheCarriedRecord()
    {
        var unreadable = SpecSetOnBranch.Unreadable("set.yaml is on the ticket branch and did not parse");
        var record = ApprovedSets.Record(Key, ApprovedSets.Noon, ["p19106x"]);

        var decision = _sut.Decide(unreadable, Filed(), null, new PipelineContext(), Key, record);

        decision.Set.Should().BeNull("a copy standing in for an unreadable set is how an edit disappears");
        decision.Handback!.Case.Should().Be(SpecHandbackCase.SpecificationMissingFromBranch);
    }

    [Fact]
    public void SpecSource_NothingAtThePathAndARecord_IsHandedTheRecordsSetOnce()
    {
        var record = ApprovedSets.Record(Key, ApprovedSets.Noon, ["p19106a", "p19106b"]);

        var decision = _sut.Decide(
            SpecSetOnBranch.Nothing, Filed(), null, new PipelineContext(), Key, record);

        decision.Source.Should().Be(SpecSource.Approved);
        decision.NeedsModel.Should().BeFalse();
        decision.Handback.Should().BeNull("an unwritten branch is a hand-off, not a broken one");
        decision.Set!.Phases.Select(p => p.PhaseId).Should().Equal("p19106a", "p19106b");
        decision.Cause.Should().Be($"{SpecRevisionCause.Approval} session-1");
    }

    [Fact]
    public void SpecSource_AnUnstampedTicketWithNoBranchSet_StillDerives()
    {
        var decision = _sut.Decide(
            SpecSetOnBranch.Nothing, Ticket("Fix the boundary check."), null,
            new PipelineContext(), Key);

        decision.Source.Should().Be(SpecSource.Derived);
        decision.NeedsModel.Should().BeTrue("a ticket nobody approved derives exactly as before");
        decision.Handback.Should().BeNull();
    }

    [Fact]
    public void SpecSource_AHandWrittenPhaseTicket_StillReadsItsDescription()
    {
        var decision = _sut.Decide(
            SpecSetOnBranch.Nothing, Ticket(EmbeddedSpec, ["phase"]), null,
            new PipelineContext(), Key);

        decision.Source.Should().Be(SpecSource.TicketDescription);
        decision.Set!.Phases.Should().ContainSingle().Which.PhaseId.Should().Be("p9999");
    }

    /// <summary>set.yaml carries the approval, which is what keeps an approved set from being
    /// re-cut once the record is no longer a source.</summary>
    [Fact]
    public void SetYaml_TheApproval_RoundTripsThroughTheOneSerializer()
    {
        var index = new SpecSetIndex();
        var set = ApprovedSets.Set(
            Key, [ApprovedSets.Phase("p0001a")], ApprovedSets.Approval(ApprovedSets.Noon, "session-9"))
            with { Revisions = [new SpecRevision(1, "initial derivation", ApprovedSets.Noon)] };

        var read = index.ApprovalOf(index.Parse(index.Serialize(set))!);

        read.Should().Be(set.Approval);
    }

    [Fact]
    public void SetYaml_ASetNobodyApproved_ReadsBackWithNoApproval()
    {
        var index = new SpecSetIndex();
        var set = ApprovedSets.Set(Key, [ApprovedSets.Phase("p0001a")], approval: null, SpecSource.Derived)
            with { Revisions = [new SpecRevision(1, "initial derivation", ApprovedSets.Noon)] };

        index.ApprovalOf(index.Parse(index.Serialize(set))!).Should().BeNull();
    }

    [Fact]
    public void SpecMarkdown_ApprovedSource_NamesTheConversation()
    {
        var set = ApprovedSets.Set(
            Key, [ApprovedSets.Phase("p0001a")], ApprovedSets.Approval(ApprovedSets.Noon, "session-42"))
            with { Revisions = [new SpecRevision(1, "approved in design conversation session-42", ApprovedSets.Noon)] };

        SpecMarkdown.Render(set).Should().Contain("approved in design conversation session-42")
            .And.NotContain("derived from the ticket");
    }

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

    private static Ticket Filed() =>
        Ticket("Migrate the client.", ["phase", FiledTicketLabels.ApprovedSetStamp]);

    private static Ticket Ticket(string description, IReadOnlyList<string>? labels = null) =>
        new(new TicketId("19106"), "A ticket", description, null, "open", "azdo", labels ?? []);

    private static SpecSetOnBranch Branch(DateTimeOffset approvedAt, IReadOnlyList<string> phaseIds) =>
        SpecSetOnBranch.Answered(new SpecSetReadResult(
            new SpecSet(
                Key,
                [.. phaseIds.Select(id => ApprovedSets.Phase(id, $"Goal {id} as it ran"))],
                SpecAccounting.Empty,
                [new SpecRevision(1, "initial derivation", ApprovedSets.Noon)],
                SpecSource.BranchArtifact,
                Approval: ApprovedSets.Approval(approvedAt)),
            "branch-sha"));
}
