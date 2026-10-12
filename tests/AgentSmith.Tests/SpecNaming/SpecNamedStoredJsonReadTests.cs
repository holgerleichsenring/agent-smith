using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecNaming;

/// <summary>
/// 2026-10-12-967aa/967ab: every JSON column, queue payload and model answer this release reads, in
/// the shape the release before the rename wrote, reads back identically to the shape this release
/// writes.
/// </summary>
public sealed class SpecNamedStoredJsonReadTests
{
    [Fact]
    public void ApprovedSeries_OldShape_ReadsFullSet()
    {
        var phases = new[] { ApprovedSets.Spec("s1a"), ApprovedSets.Spec("s1b") };
        var accounting = new SpecAccounting([new CarriedSegment(1, "s1a")], [], []);
        var set = new SpecSet("k", phases, accounting, [new SpecRevision(1, "derived", ApprovedSets.Noon)], SpecSource.Approved,
            ExecutedSpecIds: ["s1a"], Approval: ApprovedSets.Approval(ApprovedSets.Noon));
        var written = SpecApprovalJson.Write(new SpecApprovalRecord("k", set, ["api"]));

        var fromNew = SpecApprovalJson.Read(written)!;
        var fromOld = SpecApprovalJson.Read(SpecShape.Of(written))!;

        fromOld.Set.Specs.Select(p => p.SpecId).Should().Equal("s1a", "s1b");
        fromOld.Set.Executed.Should().Equal("s1a");
        fromOld.Set.Accounting.Carried.Single().SpecId.Should().Be("s1a");
        fromOld.Should().BeEquivalentTo(fromNew);
    }

    [Fact]
    public void OutcomeProposalJson_OldKind_Reads()
    {
        var proposal = new SpecOutcome(new SpecDraft("s1", "Read both", "spec: s1", []))
        {
            Findings = [new ProposalFinding("s1", "p", "w")],
        };
        var written = OutcomeProposalJson.Write(proposal);
        var renames = new Dictionary<string, string>(SpecShape.Ids) { ["spec"] = "phase" };
        var oldShape = SpecShape.Of(written, renames).Replace("\"kind\":\"spec\"", "\"kind\":\"phase\"");

        var read = OutcomeProposalJson.Read(oldShape);

        read.Should().BeOfType<SpecOutcome>().Which.Draft.SpecId.Should().Be("s1");
        read.Should().BeEquivalentTo(OutcomeProposalJson.Read(written));
    }

    [Fact]
    public void Transcript_OldProposalTurnKind_ReadsAsSpecProposal()
    {
        var turns = new[] { new TranscriptTurn(TranscriptRole.Assistant, "x", ApprovedSets.Noon, SpecDialogTurnKind.SpecProposal) };
        var written = SpecDialogSessionMapper.WriteTranscript(turns);

        var read = SpecDialogSessionMapper.ReadTranscript(written.Replace("specProposal", "phaseProposal"));

        written.Should().Contain("specProposal");
        read.Single().Kind.Should().Be(SpecDialogTurnKind.SpecProposal);
    }

    [Fact]
    public void LatestFiling_OldKind_ReadsAsSpecKind()
    {
        var store = new SpecDialogLatestOutcomeStore(null!, NullLogger<SpecDialogLatestOutcomeStore>.Instance);
        var session = new SpecDialogSession
        {
            ThreadId = "t1",
            LatestFilingJson = """{"filed":[{"reference":"R-1","title":"T"}],"error":null,"at":"2026-10-12T09:00:00Z","kind":"phase"}""",
        };

        store.Of(session).Filing!.Kind.Should().Be(SpecDialogProposalComposer.SpecKind);
    }

    [Fact]
    public void RunStory_PlannedStepAndDeclinedCriterion_ReadTheOldNames()
    {
        var planned = RunStoryJson.Serialize(new List<PlannedStepView> { new(4, "SelectSpecCommand", "Start", "s1") });
        var acceptance = RunStoryJson.Serialize(
            new AcceptanceView([], "met", "operator", Declined: [new DeclinedCriterionView("c", "r", "s1")]));
        var renames = new Dictionary<string, string>(SpecShape.Ids) { ["spec"] = "phase" };

        RunStoryJson.TryDeserialize<List<PlannedStepView>>(SpecShape.Of(planned))!.Single().SpecId.Should().Be("s1");
        RunStoryJson.TryDeserialize<AcceptanceView>(SpecShape.Of(acceptance, renames))!.Declined!.Single().Spec
            .Should().Be("s1");
    }

    [Fact]
    public void CutReviewAnswer_OldIdKey_ReadsAsSpecId()
    {
        var answer = """[{"phase_id": "s1", "criterion": "c", "problem": "p", "why": "w"}]""";

        SpecCutAnswerReader.Read(answer)!.Single().SpecId.Should().Be("s1");
    }
}
