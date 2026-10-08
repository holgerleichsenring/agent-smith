using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Webhooks;
using FluentAssertions;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-08-e8b9d: unresolved or new, from people, ours only when answered.</summary>
public sealed class PrReviewSelectionTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly PreviousAttempt Attempt = new("run-1", "success", Start, true);

    private static PrReviewNote Note(int minutes, string body = "please rename") =>
        new(new PrCommentAuthor("u", "r", "alice", "alice"), false, false, Start.AddMinutes(minutes), body);

    private static PrReviewThread Thread(bool? resolved, params PrReviewNote[] notes) => new("a.cs", 3, resolved, notes);

    [Fact]
    public void Select_ResolvedThreadNoNewNote_Dropped() =>
        PrReviewSelection.Select([Thread(true, Note(-30))], Attempt).Should().BeEmpty();

    [Fact]
    public void Select_ResolvedThreadNoteAfterAttempt_Kept() =>
        PrReviewSelection.Select([Thread(true, Note(-30), Note(30))], Attempt).Should().HaveCount(1);

    [Fact]
    public void Select_UnresolvedOldThread_Kept() =>
        PrReviewSelection.Select([Thread(false, Note(-30))], Attempt).Should().HaveCount(1);

    [Fact]
    public void Select_TopLevelCommentBeforeAttempt_Dropped() =>
        PrReviewSelection.Select([Thread(null, Note(-30))], Attempt).Should().BeEmpty();

    [Fact]
    public void Select_ThreadOnlyOurNotes_Dropped() =>
        PrReviewSelection.Select([Thread(false, Note(30, "<!-- agentsmith:pr-review:a.cs:3 -->\nours"))], Attempt)
            .Should().BeEmpty();

    [Fact]
    public void Select_HumanReplyToOurNote_KeepsOurs()
    {
        var selected = PrReviewSelection.Select(
            [Thread(false, Note(10, "<!-- agentsmith:pr-review:a.cs:3 -->\nours"), Note(20, "no, rename it"))], Attempt);

        selected.Single().Notes.Should().HaveCount(2);
    }
}
