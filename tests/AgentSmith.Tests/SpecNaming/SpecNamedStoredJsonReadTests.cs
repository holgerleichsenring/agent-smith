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
/// 2026-10-12-967aa: every JSON column, queue payload and model answer this release reads, in the
/// shape a later release writes, reads back identically to the shape this release writes.
/// </summary>
public sealed class SpecNamedStoredJsonReadTests
{
    [Fact]
    public void ApprovedSeries_SpecsJson_ReadsFullSet()
    {
        var phases = new[] { ApprovedSets.Spec("s1a"), ApprovedSets.Spec("s1b") };
        var accounting = new SpecAccounting([new CarriedSegment(1, "s1a")], [], []);
        var set = new SpecSet("k", phases, accounting, [new SpecRevision(1, "derived", ApprovedSets.Noon)], SpecSource.Approved,
            ExecutedPhaseIds: ["s1a"], Approval: ApprovedSets.Approval(ApprovedSets.Noon));
        var written = SpecApprovalJson.Write(new SpecApprovalRecord("k", set, ["api"]));

        var fromOld = SpecApprovalJson.Read(written)!;
        var fromNew = SpecApprovalJson.Read(SpecShape.Of(written))!;

        fromNew.Set.Phases.Select(p => p.PhaseId).Should().Equal("s1a", "s1b");
        fromNew.Set.Executed.Should().Equal("s1a");
        fromNew.Set.Accounting.Carried.Single().PhaseId.Should().Be("s1a");
        fromNew.Should().BeEquivalentTo(fromOld);
    }

    [Fact]
    public void OutcomeProposalJson_SpecKind_Reads()
    {
        var proposal = new SpecOutcome(new PhaseDraft("s1", "Read both", "spec: s1", []))
        {
            Findings = [new ProposalFinding("s1", "p", "w")],
        };
        var written = OutcomeProposalJson.Write(proposal);
        var renames = new Dictionary<string, string>(SpecShape.Ids) { ["phase"] = "spec" };
        var newShape = SpecShape.Of(written, renames).Replace("\"kind\":\"phase\"", "\"kind\":\"spec\"");

        var read = OutcomeProposalJson.Read(newShape);

        read.Should().BeOfType<SpecOutcome>().Which.Draft.PhaseId.Should().Be("s1");
        read.Should().BeEquivalentTo(OutcomeProposalJson.Read(written));
    }

    [Fact]
    public void Transcript_SpecProposalTurnKind_ReadsAsPhaseProposal()
    {
        var turns = new[] { new TranscriptTurn(TranscriptRole.Assistant, "x", ApprovedSets.Noon, SpecDialogTurnKind.PhaseProposal) };
        var written = SpecDialogSessionMapper.WriteTranscript(turns);

        var read = SpecDialogSessionMapper.ReadTranscript(written.Replace("phaseProposal", "specProposal"));

        written.Should().Contain("phaseProposal");
        read.Single().Kind.Should().Be(SpecDialogTurnKind.PhaseProposal);
    }

    [Fact]
    public void LatestFiling_SpecKind_ReadsAsPhaseKind()
    {
        var store = new SpecDialogLatestOutcomeStore(null!, NullLogger<SpecDialogLatestOutcomeStore>.Instance);
        var session = new SpecDialogSession
        {
            ThreadId = "t1",
            LatestFilingJson = """{"filed":[{"reference":"R-1","title":"T"}],"error":null,"at":"2026-10-12T09:00:00Z","kind":"spec"}""",
        };

        store.Of(session).Filing!.Kind.Should().Be(SpecDialogProposalComposer.SpecKind);
    }

    [Fact]
    public void RunStory_PlannedStepAndDeclinedCriterion_ReadTheSpecNames()
    {
        var planned = RunStoryJson.Serialize(new List<PlannedStepView> { new(4, "SelectPhaseCommand", "Start", "s1") });
        var acceptance = RunStoryJson.Serialize(
            new AcceptanceView([], "met", "operator", Declined: [new DeclinedCriterionView("c", "r", "s1")]));
        var renames = new Dictionary<string, string>(SpecShape.Ids) { ["phase"] = "spec" };

        RunStoryJson.TryDeserialize<List<PlannedStepView>>(SpecShape.Of(planned))!.Single().PhaseId.Should().Be("s1");
        RunStoryJson.TryDeserialize<AcceptanceView>(SpecShape.Of(acceptance, renames))!.Declined!.Single().Phase
            .Should().Be("s1");
    }

    [Fact]
    public void CutReviewAnswer_SpecIdKey_ReadsAsPhaseId()
    {
        var answer = """[{"spec_id": "s1", "criterion": "c", "problem": "p", "why": "w"}]""";

        SpecCutAnswerReader.Read(answer)!.Single().SpecId.Should().Be("s1");
    }
}
