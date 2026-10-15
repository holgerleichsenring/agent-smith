using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Prompts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Prompts;

/// <summary>
/// 2026-09-07-bd7a: "answered" is one predicate over the ticket thread, read by the
/// question pin and the contradiction repeat guard alike — the last of our comments
/// carrying a marker, followed by any comment that is not ours.
/// </summary>
public sealed class OwnTicketCommentTests
{
    private const string Marker = "the requirement contradicts what is in the repository";
    private static readonly DateTimeOffset T0 = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

    private static TicketComment Ours(int minute, string marker = Marker) =>
        new("agent-smith", T0.AddMinutes(minute), $"## Agent Smith — {marker}\n\nreason");

    private static TicketComment Theirs(int minute) =>
        new("operator", T0.AddMinutes(minute), "it lives in the shared module");

    [Fact]
    public void IsAnswered_ACommentByOthersAfterOurs_IsTrue() =>
        OwnTicketComment.IsAnswered([Ours(0), Theirs(5)], Marker).Should().BeTrue();

    [Fact]
    public void IsAnswered_OursIsTheLastWord_IsFalse() =>
        OwnTicketComment.IsAnswered([Theirs(0), Ours(5)], Marker).Should().BeFalse();

    [Fact]
    public void IsAnswered_OnlyOurOwnCommentsFollow_IsFalse() =>
        OwnTicketComment.IsAnswered([Ours(0), Ours(5, "the ticket reads two ways")], Marker).Should().BeFalse();

    [Fact]
    public void IsAnswered_ReadsTheLastOfOurMarkedComments_NotTheFirst() =>
        OwnTicketComment.IsAnswered([Ours(0), Theirs(5), Ours(10)], Marker).Should().BeFalse();

    [Fact]
    public void IsAnswered_OthersCommentedButNoneOfOursCarriesTheMarker_IsTrue() =>
        OwnTicketComment.IsAnswered([Theirs(0)], Marker).Should().BeTrue();

    [Fact]
    public void IsAnswered_NoThread_IsFalse() =>
        OwnTicketComment.IsAnswered(null, Marker).Should().BeFalse();

    [Fact]
    public void IsAnswered_OrdersByTime_NotByListPosition() =>
        OwnTicketComment.IsAnswered([Ours(5), Theirs(0)], Marker).Should().BeFalse();

    private static PrReviewNote PrNote(int minute, string body = "use the shared module", bool ours = false) =>
        new(new PrCommentAuthor("u", "r", "reviewer", "reviewer"), false, false, T0.AddMinutes(minute), body) { IsOurs = ours };

    [Fact]
    public void OwnTicketComment_TrustedPrReplyAfterHandback_IsForeign()
    {
        var foreign = OwnTicketComment.ForeignAfter([Ours(0)], Marker, [PrNote(5)]).ToList();

        foreign.Should().ContainSingle().Which.Body.Should().Be("use the shared module");
        OwnTicketComment.IsAnswered([Ours(0)], Marker, [PrNote(5)]).Should().BeTrue();
    }

    [Fact]
    public void OwnTicketComment_PrNoteBeforeHandbackOrOurs_IsNotForeign() =>
        OwnTicketComment.IsAnswered([Ours(10)], Marker, [PrNote(5), PrNote(15, "<!-- agentsmith:handback -->", ours: true)])
            .Should().BeFalse();

    [Fact]
    public void PrNotes_ReadsEveryThreadOfTheFeedback()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.PrReviewFeedback, (IReadOnlyList<PrReviewFeedback>)[
            new PrReviewFeedback("api", "https://github.com/o/api/pull/4",
                [new PrReviewThread(null, null, null, [PrNote(1)]), new PrReviewThread("a.cs", 2, false, [PrNote(2), PrNote(3)])])]);

        OwnTicketComment.PrNotes(pipeline).Should().HaveCount(3);
    }

    private const string LegacyWorkingStatus = "Agent Smith is working on this issue...";

    [Fact]
    public void OwnTicketComment_WorkingStatus_IsOurs()
    {
        OwnTicketComment.IsOurs(OwnTicketComment.WorkingStatus).Should().BeTrue();
        OwnTicketComment.IsOurs(new TicketComment("operator", T0, OwnTicketComment.WorkingStatus)).Should().BeTrue();
    }

    [Fact]
    public void OwnTicketComment_LegacyWorkingStatus_IsOurs()
    {
        OwnTicketComment.IsOurs(LegacyWorkingStatus).Should().BeTrue(
            "comments already posted on tickets carry the old text");
        OwnTicketComment.IsOurs(new TicketComment("operator", T0, LegacyWorkingStatus)).Should().BeTrue();
    }

    [Fact]
    public void OwnTicketComment_EveryCommentPipelineErrorHandlerPosts_IsOurs()
    {
        var failure = new FailureTicketComment();
        string[] posted =
        [
            OwnTicketComment.WorkingStatus,
            failure.ForStep(CommandResult.Fail("step broke"), new PipelineContext()),
            failure.ForFatal(new InvalidOperationException("sandbox quota")),
            failure.ForMessage("cancelled"),
        ];

        posted.Should().AllSatisfy(body => OwnTicketComment.IsOurs(body).Should().BeTrue(body));
    }

    [Fact]
    public void OwnTicketComment_WorkingCommentAfterHandback_IsNotAnAnswer()
    {
        var working = new TicketComment("operator", T0.AddMinutes(10), OwnTicketComment.WorkingStatus);
        var legacy = new TicketComment("operator", T0.AddMinutes(20), LegacyWorkingStatus);

        OwnTicketComment.IsAnswered([Ours(0), working, legacy], Marker).Should().BeFalse(
            "the next run's working comment is ours, not a person's reply");
    }
}
