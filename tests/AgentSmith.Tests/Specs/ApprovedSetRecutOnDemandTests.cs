using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-22-8b25: an approved set is immune to the tracker, and this is the one deliberate door
/// through it. A comment opening with the reserved phrase cuts the set again; every other comment
/// and every edit still leaves it standing. Run through the real reader, the real precedence and
/// the real publisher, because what the door has to survive is the round trip: the re-cut set is
/// read back off the branch next run, and a re-cut published without an approval would lose the
/// immunity after exactly one demand.
/// </summary>
public sealed class ApprovedSetRecutOnDemandTests
{
    private const string Key = "azdo-19106";
    private const string Text = "Migrate the client.";
    private static readonly DateTimeOffset Approved = ApprovedSets.Noon;
    private static readonly DateTimeOffset Demanded = ApprovedSets.Noon.AddHours(3);

    [Fact]
    public async Task RevisionCause_AnApprovedSetAndADemandComment_IsRecut()
    {
        var harness = Harness();
        harness.Comments = [OurCut(), Demand()];

        var result = await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        result.IsSuccess.Should().BeTrue();
        harness.Deriver.Calls.Should().Be(1, "a person asked for this set to be cut again");
        harness.Writer.Written!.Current.Cause.Should().Be(SpecRevisionCause.RecutDemand,
            "the history must say why an approved set changed");
        harness.Writer.Written.Phases.Select(p => p.PhaseId).Should().Equal("p19106z");
    }

    [Fact]
    public async Task RevisionCause_AnApprovedSetAndAnOrdinaryComment_IsStillKept()
    {
        var harness = Harness();
        harness.Comments = [OurCut(), Ordinary("phase b is wrong: the callers stay where they are")];

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Deriver.Calls.Should().Be(0, "only the reserved phrase is new; a remark is a remark");
        harness.Writer.Written!.Current.Cause.Should()
            .Be($"{SpecRevisionCause.Comment} — {ApprovedSetKept.Kept}");
    }

    [Fact]
    public async Task RevisionCause_AnApprovedSetAndAnEdit_IsStillKept()
    {
        var harness = Harness(fingerprint: "the-text-before-the-edit");

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Deriver.Calls.Should().Be(0);
        harness.Writer.Written!.Current.Cause.Should()
            .Be($"{SpecRevisionCause.TicketEdit} — {ApprovedSetKept.Kept}");
    }

    /// <summary>
    /// A person who demands a re-cut and fixes the ticket text in the same breath must get the
    /// re-cut, not a notice telling them their edit was kept out.
    /// </summary>
    [Fact]
    public async Task RevisionCause_ADemandAndAnEdit_TheDemandOutranksTheEdit()
    {
        var harness = Harness(fingerprint: "the-text-before-the-edit");
        harness.Comments = [OurCut(), Demand()];

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Writer.Written!.Current.Cause.Should().Be(SpecRevisionCause.RecutDemand);
        harness.Deriver.Calls.Should().Be(1);
    }

    [Fact]
    public async Task RevisionCause_ADemandWhileResuming_IsIgnored()
    {
        var harness = Harness();
        harness.Comments = [OurCut(), Demand()];
        var context = harness.Context(Ticket());
        context.Pipeline.Set(ContextKeys.ResumeCheckpoint, "{}");

        await harness.Handler().ExecuteAsync(context, default);

        harness.Deriver.Calls.Should().Be(0, "a resume continues what the run was doing");
        harness.Writer.Written!.Current.Cause.Should().Be(SpecRevisionCause.Resume);
    }

    /// <summary>
    /// The set the model produces carries no approval of its own. Published as it stands, the next
    /// run would read an UNAPPROVED set off the branch and the next ordinary comment would re-cut
    /// it — the immunity gone after exactly one demand.
    /// </summary>
    [Fact]
    public async Task RecutOnDemand_ThePublishedSet_StaysApprovedToTheDemander()
    {
        var harness = Harness();
        var demand = Demand();
        harness.Comments = [OurCut(), demand];

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        var written = harness.Writer.Written!;
        written.Approval.Should().NotBeNull("a re-cut set is still a set somebody stands behind");
        written.Approval!.Principal.Should().Be(demand.Author);
        written.Approval.At.Should().Be(demand.CreatedAt);
        new SpecSetIndex().Serialize(written).Should().Contain("approved_by: sample.operator",
            "the approval reaches the branch, which is the only set the next run reads");
    }

    [Fact]
    public async Task RecutOnDemand_ADemandAlreadyActedOn_DoesNotFireAgainOnTheNextRun()
    {
        var first = Harness();
        var demand = Demand();
        first.Comments = [OurCut(), demand];

        await first.Handler().ExecuteAsync(first.Context(Ticket()), default);

        var recorded = first.Writer.Written!.Approval!.At;
        var next = Harness(approvedAt: recorded);
        next.Comments = [OurCut(), demand];

        await next.Handler().ExecuteAsync(next.Context(Ticket()), default);

        next.Deriver.Calls.Should().Be(0, "the same demand must not re-cut the set on every run");
        next.Writer.Written!.Current.Cause.Should().NotBe(SpecRevisionCause.RecutDemand);
    }

    /// <summary>
    /// A re-cut that HANDS BACK posts no cut comment, so the anchor never moves past the demand.
    /// The approval the demand recorded is what clears it there — without it the hand-back would
    /// re-fire on every later run of that ticket.
    /// </summary>
    [Fact]
    public async Task RecutOnDemand_ARecutThatHandsBack_StillClearsTheDemand()
    {
        var harness = Harness();
        harness.Deriver.Result = HandedBack();
        var demand = Demand();
        harness.Comments = [OurCut(), demand];

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Writer.Written!.IsHandedBack.Should().BeTrue();
        harness.Cuts.Comments.Should().BeEmpty("a hand-back posts no cut comment to move the anchor");
        harness.Writer.Written.Approval!.At.Should().Be(demand.CreatedAt);
        SpecRecutDemand.In([OurCut(), demand], harness.Writer.Written.Approval).Should().BeNull(
            "the demand is cleared by its own timestamp, not by anchor arithmetic");
    }

    /// <summary>The cut comment tells the author a demand was acted on — the author learns it
    /// from the ticket, without opening the branch.</summary>
    [Fact]
    public async Task RecutOnDemand_TheCutComment_SaysADemandCausedIt()
    {
        var harness = Harness();
        harness.Tracker = harness.Notices.Tracker;
        harness.Comments = [OurCut(), Demand()];

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Cuts.Comments.Should().ContainSingle()
            .Which.Should().Contain("A re-cut was demanded on the ticket after");
    }

    /// <summary>
    /// A phase that ran is never edited, and the demand can only reach the tail. That is ENFORCED
    /// rather than asked for: the model is handed the previous set, and SpecDerivationParser
    /// re-uses its executed head verbatim while discarding the model's entries for those
    /// positions (pinned by Recut_UnexecutedTail_IsRepartitionedWhileTheExecutedHeadKeepsItsIds).
    /// What this pins is the wiring — a demand really does put that head in front of the parser.
    /// </summary>
    [Fact]
    public async Task RecutOnDemand_ThePhaseThatAlreadyRan_IsHandedToTheModelAsTheExecutedHead()
    {
        var harness = Harness(executed: "p19106a");
        harness.Comments = [OurCut(), Demand()];

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Deriver.CauseSeen.Should().Be(SpecRevisionCause.RecutDemand);
        harness.Deriver.PreviousSeen!.ExecutedHead.Select(p => p.PhaseId).Should().Equal(
            ["p19106a"],
            "the parser re-uses that head verbatim — a demand can only reach the unstarted tail");
    }

    /// <summary>A demand is ACTED on, so there is nothing to report as kept — the kept-set notice
    /// exists for input the run ignored.</summary>
    [Fact]
    public async Task RecutOnDemand_PostsNoKeptSetNotice()
    {
        var harness = Harness();
        harness.Tracker = harness.Notices.Tracker;
        harness.Comments = [OurCut(), Demand()];

        await harness.Handler().ExecuteAsync(harness.Context(Ticket()), default);

        harness.Notices.Comments.Should().BeEmpty(
            "telling somebody their demand was kept out would be the opposite of what happened");
    }

    private static ApprovedSetHarness Harness(
        string? fingerprint = null, string? executed = null, DateTimeOffset? approvedAt = null)
    {
        var harness = new ApprovedSetHarness { Branch = { Key = Key }, PointerSha = "branch-sha" };
        harness.Deriver.Result = Recut();
        harness.Branch.SeedSet(
            BranchYaml(fingerprint ?? TicketTextFingerprint.Of(Ticket()), executed, approvedAt ?? Approved),
            new Dictionary<string, string>
            {
                ["p19106a-first"] = PhaseYaml("p19106a"),
                ["p19106b-second"] = PhaseYaml("p19106b"),
            });
        return harness;
    }

    private static string BranchYaml(string fingerprint, string? executed, DateTimeOffset approvedAt)
    {
        var lines = new List<string>
        {
            $"key: {Key}",
            "source: Approved",
            "phases:",
            "- p19106a-first",
            "- p19106b-second",
            "revisions:",
            "- number: 1",
            "  cause: approved in design conversation session-77",
            "  at: 2026-09-17T12:00:00.0000000+00:00",
            $"ticket_fingerprint: {fingerprint}",
            $"approved_at: {approvedAt:O}",
            "approved_in_conversation: session-77",
            "approved_by: sample.approver",
        };
        if (executed is not null) lines.AddRange(["executed_phases:", $"- {executed}"]);
        return string.Join("\n", lines);
    }

    private static string PhaseYaml(string id) => $"""
        phase: {id}
        goal: "Goal {id}"
        done:
          - "Done {id}."
        """;

    private static Ticket Ticket() =>
        new(new TicketId("19106"), "Migrate the client", Text, null, "open", "azdo", []);

    private static TicketComment OurCut() => new(
        "agent-smith", Approved.AddHours(1),
        $"## Agent Smith — {SpecSetComment.CutMarker}\n\nthe phases follow");

    private static TicketComment Demand() =>
        new("sample.operator", Demanded, $"{SpecSetComment.RecutDemand}\n\nThe second phase is wrong.");

    private static TicketComment Ordinary(string body) =>
        new("sample.operator", Demanded, body);

    // What the model answers a demand with: one phase that is not what the branch carried.
    private static SpecDerivation Recut(SpecHandback? handback = null)
    {
        var segments = TicketSegmenter.Segment(Text);
        var phase = new SpecPhase(
            ApprovedSets.Phase("p19106z", "Cut again on demand").Draft, "p19106z", string.Empty,
            [.. segments.Select(s => s.Id)]);
        return new SpecDerivation(
            new SpecSet(
                Key, handback is null ? [phase] : [],
                handback is null ? SpecAccountingBuilder.Build([phase], [], segments) : SpecAccounting.Empty,
                [new SpecRevision(1, SpecRevisionCause.Initial, Approved)],
                SpecSource.BranchArtifact, handback),
            []);
    }

    private static SpecDerivation HandedBack() => Recut(
        new SpecHandback(SpecHandbackCase.Question, "which of the two clients is meant?"));
}
