using AgentSmith.Application.Services.Prompts;
using AgentSmith.Domain.Entities;
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
}
