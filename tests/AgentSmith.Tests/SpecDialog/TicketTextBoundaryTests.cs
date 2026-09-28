using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-27-481ba: where the cap falls in a ticket's text, and which end of the discussion
/// survives it.
/// <para>
/// Nothing upstream orders the comments — not one of the four providers and not one of the four
/// mappers, and Azure DevOps is asked with no sort order at all — so the order used to be whatever
/// a tracker returned, and a raw prefix cut took whichever end that happened to put last, mid-word.
/// </para>
/// </summary>
public sealed class TicketTextBoundaryTests
{
    [Fact]
    public void TicketText_CommentsReturnedOutOfOrder_AreSeededNewestFirstRegardless()
    {
        // The tracker hands them back oldest-last, newest-first, shuffled — it makes no difference.
        var composed = TicketTextComposer.Compose(
            Ticket("short"),
            [Comment(3, "third"), Comment(1, "first"), Comment(2, "second")]);

        composed.Text.Should().Contain("first").And.Contain("second").And.Contain("third");
        composed.Text.IndexOf("first", StringComparison.Ordinal)
            .Should().BeLessThan(composed.Text.IndexOf("second", StringComparison.Ordinal));
        composed.Text.IndexOf("second", StringComparison.Ordinal)
            .Should().BeLessThan(composed.Text.IndexOf("third", StringComparison.Ordinal));
    }

    [Fact]
    public void TicketText_AThreadLongerThanTheCap_KeepsTheNewestWhole()
    {
        var big = new string('x', 6_000);
        var composed = TicketTextComposer.Compose(
            Ticket("short"),
            [.. Enumerable.Range(1, 6).Select(i => Comment(i, $"c{i}-{big}"))]);

        composed.Truncated.Should().BeTrue();
        // The end of the thread is where its current state is.
        composed.Text.Should().Contain("c6-").And.Contain("c5-").And.NotContain("c1-");
    }

    [Fact]
    public void TicketText_ACommentThatDoesNotFit_IsDroppedEntireNotSliced()
    {
        var big = new string('y', 12_000);
        var composed = TicketTextComposer.Compose(
            Ticket("short"), [Comment(1, $"old-{big}"), Comment(2, $"new-{big}")]);

        // Whichever survives is whole: a half-rendered comment reads like a complete one.
        composed.Text.Should().Contain("new-");
        composed.Text.Should().NotContain("old-");
        composed.Text.Length.Should().BeLessThanOrEqualTo(SeededTicketLimits.Text);
    }

    [Fact]
    public void TicketText_AHeadLongerThanTheCap_IsCutAndSaysTheTicketCanBeRead()
    {
        var composed = TicketTextComposer.Compose(
            Ticket(new string('z', SeededTicketLimits.Text + 500)), [Comment(1, "ignored")]);

        // The one place a character cut remains: the head IS the ticket, so it is kept first.
        composed.Truncated.Should().BeTrue();
        composed.Text.Should().HaveLength(SeededTicketLimits.Text);
    }

    [Fact]
    public void TicketText_TheWholeTicket_IsTheSameCompositionWithoutTheCap()
    {
        var big = new string('w', 12_000);
        var comments = new[] { Comment(1, $"old-{big}"), Comment(2, $"new-{big}") };

        var whole = TicketTextComposer.Whole(Ticket("short"), comments);

        // What a read answers from is what the seed would have been without the cap, so a slice
        // never shows the model something the seeding would have composed differently.
        whole.Should().Contain("old-").And.Contain("new-");
        whole.Length.Should().BeGreaterThan(SeededTicketLimits.Text);
    }

    [Fact]
    public void TicketConversation_ANewestCommentTooBigForItsCap_IsStillTakenWhole()
    {
        // The RUN path may overshoot: its cap bounds a prompt, not a column, and dropping the most
        // recent thing anybody said would be worse than exceeding it. Unchanged by this phase.
        var huge = new string('q', TicketConversationPromptSection.MaxChars + 5_000);

        var rendered = TicketConversationPromptSection.Render([Comment(1, huge)]);

        rendered.Length.Should().BeGreaterThan(TicketConversationPromptSection.MaxChars);
    }

    [Fact]
    public void NewestFirstFit_ForbiddenToOvershoot_CutsTheNewestAndSaysSo()
    {
        var fitted = NewestFirstFitProbe(["old", new string('n', 50)], budget: 20);

        fitted.NewestCut.Should().BeTrue();
        fitted.Kept.Should().ContainSingle().Which.Should().HaveLength(20);
        fitted.Dropped.Should().Be(1);
    }

    private static FittedEntries NewestFirstFitProbe(string[] ordered, int budget) =>
        NewestFirstFit.Of(ordered, budget, mayOvershoot: false);

    private static Ticket Ticket(string description) =>
        new(new TicketId("412"), "Widget drops", description, null, "Open", "Jira");

    private static TicketComment Comment(int day, string body) =>
        new("someone", new DateTimeOffset(2026, 9, day, 9, 0, 0, TimeSpan.Zero), body);
}
