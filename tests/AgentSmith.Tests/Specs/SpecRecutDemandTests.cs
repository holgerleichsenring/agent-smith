using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-22-8b25: the reserved phrase that asks for an approved specification to be cut again,
/// and the predicate that decides a comment carries it. The predicate is its OWN: the comment rule
/// uses its marker only to find our anchor and never looks at the foreign body, so asking it with
/// the demand phrase would read every ordinary comment ever written as a demand.
/// </summary>
public sealed class SpecRecutDemandTests
{
    private const string Key = "azdo-19106";
    private static readonly DateTimeOffset Cut = ApprovedSets.Noon;

    [Fact]
    public void RecutDemand_ThePhraseAsTheOpeningLine_IsADemand()
    {
        var demand = Foreign(
            $"{SpecSetComment.RecutDemand}\n\nThe second phase has the wrong repository.", 1);

        SpecRecutDemand.In([OurCut(), demand], null).Should().BeSameAs(demand);
    }

    /// <summary>Leading blank lines and casing are a tracker's rendering, not a different ask.</summary>
    [Fact]
    public void RecutDemand_LeadingBlankLinesAndCasing_AreStillADemand()
    {
        var demand = Foreign($"\n  \n  {SpecSetComment.RecutDemand.ToUpperInvariant()}  \n", 1);

        SpecRecutDemand.Demands(demand).Should().BeTrue();
    }

    /// <summary>
    /// The whole-line rule. A fragment matched anywhere is how the OURS list works, and that list
    /// carries its heading in two dash encodings — direct evidence trackers re-encode what they
    /// are given, so a phrase found anywhere fires on punctuation.
    /// </summary>
    [Fact]
    public void RecutDemand_ThePhraseInsideALongerLine_IsNotADemand()
    {
        SpecRecutDemand.Demands(
            Foreign($"Please {SpecSetComment.RecutDemand} when you get a chance.", 1))
            .Should().BeFalse("a remark that happens to contain the words is a remark");
    }

    [Fact]
    public void RecutDemand_ThePhraseFurtherDownTheBody_IsNotADemand()
    {
        SpecRecutDemand.Demands(
            Foreign($"Two things.\n\n{SpecSetComment.RecutDemand}\n", 1))
            .Should().BeFalse("it opens the comment or it is not deliberate");
    }

    [Fact]
    public void RevisionCause_ADemandInOurOwnComment_IsNotADemand()
    {
        var ours = new TicketComment(
            "agent-smith", Cut.AddHours(1),
            $"{SpecSetComment.RecutDemand}\n\n## Agent Smith — {SpecSetComment.CutMarker}");

        SpecRecutDemand.In([OurCut(), ours], null).Should().BeNull(
            "this system asking itself for a re-cut is not a person demanding one");
    }

    [Fact]
    public void RevisionCause_ADemandBeforeTheLastCutComment_IsNotADemand()
    {
        var stale = Foreign(SpecSetComment.RecutDemand, -1);

        SpecRecutDemand.In([stale, OurCut()], null).Should().BeNull(
            "the cut comment after it is this system answering — the demand was acted on");
    }

    /// <summary>
    /// The demand's own timestamp is what clears it, which is what a re-cut ending in a HAND-BACK
    /// needs: no cut comment is posted then, so the anchor never moves past the demand.
    /// </summary>
    [Fact]
    public void RecutDemand_ADemandNoNewerThanTheApproval_IsAlreadyActedOn()
    {
        var demand = Foreign(SpecSetComment.RecutDemand, 1);
        var recorded = SpecRecutDemand.ApprovalOf(demand)!;

        SpecRecutDemand.In([OurCut(), demand], recorded).Should().BeNull(
            "the approval it recorded is this demand — it has already been acted on");
        SpecRecutDemand.In([OurCut(), demand], ApprovedSets.Approval(demand.CreatedAt.AddMinutes(-1)))
            .Should().BeSameAs(demand, "a demand written after the approval is new input");
    }

    [Fact]
    public void RecutDemand_TheApprovalItRecords_NamesTheAuthorAndTheirTimestamp()
    {
        var demand = Foreign(SpecSetComment.RecutDemand, 1);

        var approval = SpecRecutDemand.ApprovalOf(demand)!;

        approval.Principal.Should().Be(demand.Author, "the person who demanded it approved it");
        approval.At.Should().Be(demand.CreatedAt, "the demand instant is the version marker");
        approval.Conversation.Should().BeEmpty("there was no design conversation to name");
    }

    /// <summary>
    /// The door is named exactly where a person learns their input was not acted on — and the
    /// notice carrying that name must not itself be a demand when somebody quotes it back.
    /// </summary>
    [Fact]
    public void KeptNotice_AnIgnoredInput_NamesTheDoorAndCannotItselfDemandARecut()
    {
        var body = ApprovedSetKept.Notice(Set(approved: true), SpecRevisionCause.Comment)!;

        body.Should().Contain(SpecSetComment.RecutDemand, "a door nobody can name is not a door");
        SpecRecutDemand.Demands(Foreign(body, 1)).Should().BeFalse(
            "an operator quoting the notice must not fire a re-cut nobody demanded");
    }

    [Fact]
    public void CutComment_AnApprovedSet_NamesTheDoorAndStopsClaimingNobodyCanRecut()
    {
        var body = SpecSetComment.Render(Set(approved: true), null);

        body.Should().Contain(SpecSetComment.RecutDemand);
        body.Should().NotContain("no run re-cuts a set somebody approved",
            "one now does, and an absolute posted on every run would be a falsehood on the ticket");
        SpecRecutDemand.Demands(Foreign(body, 1)).Should().BeFalse();
    }

    private static TicketComment OurCut() => new(
        "agent-smith", Cut, $"## Agent Smith — {SpecSetComment.CutMarker}\n\nphases follow");

    private static TicketComment Foreign(string body, int hoursAfterTheCut) =>
        new("sample.operator", Cut.AddHours(hoursAfterTheCut), body);

    private static SpecSet Set(bool approved) => new(
        Key,
        [ApprovedSets.Phase("p19106a")],
        SpecAccounting.Empty,
        [new SpecRevision(1, SpecRevisionCause.Initial, Cut)],
        approved ? SpecSource.Approved : SpecSource.Derived,
        Approval: approved ? ApprovedSets.Approval(Cut, "session-77") : null);
}
