using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-09-08-4aa9: what the model and the author are told about a comment — the prompt
/// names the revision the comment postdates, points at the conversation and keeps the
/// executed phases; the derivation-time comment names the re-cut and the kept phases.
/// </summary>
public sealed class CommentRecutNoticeTests
{
    [Fact]
    public void PreviousCut_AComment_NamesTheRevisionTheCommentPostdates()
    {
        var previous = Set(SpecRevisionCause.Initial, SpecRevisionCause.Retrigger);

        var section = PreviousCutPromptSection.Render(previous, SpecRevisionCause.Comment);

        section.Should().Contain("Cause of the revision you are writing now: comment on the ticket");
        section.Should().Contain("The ticket was commented on after revision 2 was cut");
        section.Should().Contain("the comment is in the ticket conversation above");
        section.Should().Contain("p1a (EXECUTED — keep exactly as it is)");
        section.Should().Contain("p1b (not started)");
    }

    [Fact]
    public void PreviousCut_AnyOtherCause_SaysNothingAboutAComment()
    {
        var section = PreviousCutPromptSection.Render(Set(SpecRevisionCause.Initial), SpecRevisionCause.Retrigger);

        section.Should().NotContain("was commented on");
    }

    [Fact]
    public void Comment_ACommentRevision_NamesTheRecutAndTheKeptPhases()
    {
        var body = SpecSetComment.Render(Set(SpecRevisionCause.Initial, SpecRevisionCause.Comment), null);

        body.Should().Contain(
            "The ticket was commented on after revision 1 was cut: p1a already ran and stayed as it was; "
            + "the rest was cut again with the comment in view.");
    }

    [Fact]
    public void Comment_ACommentBeforeAnyPhaseRan_SaysTheWholeSetWasCutAgain()
    {
        var set = Set(SpecRevisionCause.Initial, SpecRevisionCause.Comment) with { Executed = [] };

        SpecSetComment.Render(set, null).Should().Contain(
            "no phase had run yet, so the whole set was cut again with the comment in view.");
    }

    [Fact]
    public void Comment_TheHeading_CarriesTheCutMarkerAndReadsAsOurs()
    {
        var body = SpecSetComment.Render(Set(SpecRevisionCause.Initial), null);
        var posted = new TicketComment("agent-smith", DateTimeOffset.UtcNow, body);

        body.Should().Contain(SpecSetComment.CutMarker);
        OwnTicketComment.IsOurs(posted).Should().BeTrue();
        OwnTicketComment.AwaitsAnswer(posted).Should().BeFalse("the cut comment asks for nothing");
        body.Should().NotContain("was commented on");
    }

    private static SpecSet Set(params string[] causes) => new(
        "azdo-1",
        [Phase("p1a", "Introduce the guard"), Phase("p1b", "Move the callers")],
        SpecAccounting.Empty,
        [.. causes.Select((cause, i) => new SpecRevision(i + 1, cause, DateTimeOffset.UtcNow))],
        SpecSource.BranchArtifact,
        ExecutedPhaseIds: ["p1a"]);

    private static SpecPhase Phase(string id, string goal) => new(
        new PhaseDraft(id, goal, $"phase: {id}\ngoal: \"{goal}\"", []) { Done = [$"{goal} is done."] },
        id, string.Empty, []);
}
