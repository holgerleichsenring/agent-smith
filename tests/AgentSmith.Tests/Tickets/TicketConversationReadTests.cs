using System.Text.Json;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Moq;
using static AgentSmith.Tests.Tickets.TicketConversationFixtures;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// 2026-10-13-9a59h: one read of a ticket's conversation over the real registration and a migrated store —
/// recorded questions, edits and hand-backs, pull-request entries through the read port, the cached live
/// thread, and the grouping by attempt.
/// </summary>
public sealed class TicketConversationReadTests : IDisposable
{
    private readonly TicketStoreHarness _h = new();

    public TicketConversationReadTests() => Thread();

    public void Dispose() => _h.Dispose();

    private void Thread(params TicketComment[] comments) =>
        _h.Tracker.Setup(t => t.GetCommentsAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ReturnsAsync(comments);

    private async Task<TicketConversationView> ReadAsync(bool mayReadTranscript = true) =>
        (await _h.Conversation.ReadAsync("alpha", "42", mayReadTranscript, default))!;

    private static string Json(object? detail) => JsonSerializer.Serialize(detail);

    [Fact]
    public async Task TicketConversation_QuestionWithDeadlineAnswer_MarkedDefault()
    {
        await _h.AddAsync(Attempt("r-1", 0), Row("r-1", 3, RunConversationKinds.Question, 5, "Which branch?", "q-1"),
            Answer("r-1", "q-1", "main", "system", "timeout", 60));

        var entries = (await ReadAsync()).Entries;

        entries.Should().ContainSingle(e => e.Kind == RunConversationKinds.Question).Which.Should().Match<TicketConversationEntryView>(
            e => e.Id == "question:r-1:q-1" && e.Role == TicketConversationRoles.Agent && e.AttemptRunId == "r-1");
        var answer = entries.Should().ContainSingle(e => e.Kind == TicketConversationKinds.DefaultAnswer).Subject;
        (answer.Role, answer.Author, answer.Text).Should().Be((TicketConversationRoles.System, (string?)null, "main"));
    }

    [Fact]
    public async Task TicketConversation_FingerprintChanged_ShowsCriteriaDiffWithoutAuthor()
    {
        await _h.AddAsync(Attempt("r-1", 0), Attempt("r-2", 60), Fetched("r-1", "a"), Fetched("r-2", "b"),
            Row("r-1", 1, RunConversationKinds.TicketText, 0, "Export it.", detailJson: "{\"acceptanceCriteria\":\"In a minute.\"}"),
            Row("r-2", 1, RunConversationKinds.TicketText, 60, "Export it.", detailJson: "{\"acceptanceCriteria\":\"In a second.\"}"));

        var edit = (await ReadAsync()).Entries.Should().ContainSingle(e => e.Kind == TicketConversationKinds.Edited).Subject;

        (edit.Author, edit.AttemptRunId, edit.At).Should().Be(((string?)null, "r-2", T.AddMinutes(60)));
        Json(edit.Detail).Should().Contain("In a minute.").And.Contain("In a second.").And.NotContain("title").And.NotContain("description");
    }

    [Fact]
    public async Task TicketConversation_PrReviewEntry_CarriesFileAndLine()
    {
        await _h.AddAsync(Attempt("r-1", 0), Record("7"), Note("7", "c-1", "Rename this.", 10, file: "src/A.cs", line: 12));

        var entry = (await ReadAsync()).Entries.Should().ContainSingle(e => e.Source == TicketConversationSources.PrReview).Subject;

        (entry.File, entry.Line, entry.Url, entry.Author, entry.Role).Should().Be(("src/A.cs", (int?)12, PrUrl("7"), "bob", TicketConversationRoles.Person));
    }

    [Fact]
    public async Task TicketConversation_CommandStartedNothing_LabelledNoAttempt()
    {
        await _h.AddAsync(Attempt("r-1", 0), Record("7"), Note("7", "c-2", PrCommandTexts.NoHolder("42"), 10, author: "agent-smith", ours: true));

        var entry = (await ReadAsync()).Entries.Single(e => e.Source == TicketConversationSources.PrReview);

        entry.Kind.Should().Be(TicketConversationKinds.CommandAnswer);
        Json(entry.Detail).Should().Contain("\"StartedNothing\":true");
    }

    [Fact]
    public async Task TicketConversation_CommandStartedRun_AttachedToThatAttempt()
    {
        await _h.AddAsync(Attempt("r-1", 0), Attempt("r-2", 60), Record("7"),
            Note("7", "c-3", PrCommandTexts.Started("code", "r-2"), 30, author: "agent-smith", ours: true));

        var entry = (await ReadAsync()).Entries.Single(e => e.Source == TicketConversationSources.PrReview);

        (entry.Kind, entry.AttemptRunId).Should().Be((TicketConversationKinds.CommandAnswer, "r-2"),
            "the answer names the run it started, although it was posted before that run read the ticket");
    }

    [Fact]
    public async Task TicketConversation_TrackerDown_ThreadUnavailable_RestServed()
    {
        _h.Tracker.Setup(t => t.GetCommentsAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("down"));
        await _h.AddAsync(Attempt("r-1", 0), Row("r-1", 3, RunConversationKinds.Question, 5, "Which branch?", "q-1"));

        var view = await ReadAsync();

        view.ThreadUnavailable.Should().BeTrue();
        view.Entries.Should().ContainSingle(e => e.Kind == RunConversationKinds.Question);
    }

    [Fact]
    public async Task TicketConversation_WorkerLaterAnswerComment_IsOursTicketEntry()
    {
        await _h.AddAsync(Attempt("r-1", 0));
        Thread(Comment("c-5", "agent-smith", 40, "Agent Smith — this follow-up was not started: the approval was refused."));

        var entry = (await ReadAsync()).Entries.Should().ContainSingle().Subject;

        (entry.Source, entry.Role, entry.IsOurs, entry.Id).Should().Be(
            (TicketConversationSources.Ticket, TicketConversationRoles.Agent, true, "ticket:c-5"));
    }

    [Fact]
    public async Task TicketConversation_SystemPrNote_RoleSystem()
    {
        await _h.AddAsync(Attempt("r-1", 0), Record("7"), Note("7", "t-1/c-1", "Alice voted -5", 10, author: "alice", system: true));

        var entry = (await ReadAsync()).Entries.Single();

        (entry.Role, entry.Author).Should().Be((TicketConversationRoles.System, (string?)null));
    }

    [Fact]
    public async Task TicketConversation_DecisionWithBody_OneEntryIdIsNoteHostId()
    {
        await _h.AddAsync(Attempt("r-1", 0), Record("7"),
            Note("7", "rv-9", "Looks right.", 10, kind: PrReviewEntryKinds.Review, decision: PrReviewDecisions.Approved));

        var entry = (await ReadAsync()).Entries.Should().ContainSingle().Subject;

        entry.Id.Should().Be($"pr-review:{PrKey.Of(PrUrl("7"))}:rv-9");
        (entry.Role, entry.Text).Should().Be((TicketConversationRoles.Reviewer, "Looks right."));
        Json(entry.Detail).Should().Contain(PrReviewDecisions.Approved);
    }

    [Fact]
    public async Task TicketConversation_EntryIds_JoinCauseSourcesIncludingTwoPrsWithOneHostId()
    {
        await _h.AddAsync(Attempt("r-1", 0), Record("7"), Record("8"), Note("7", "1", "Here.", 10), Note("8", "1", "There.", 11));

        var entries = (await ReadAsync()).Entries;

        entries.Select(e => e.Id).Should().OnlyHaveUniqueItems().And.HaveCount(2);
        entries.Select(e => (e.SourceId, e.PrKey, e.Url)).Should().BeEquivalentTo(
            [("1", PrKey.Of(PrUrl("7")), PrUrl("7")), ("1", PrKey.Of(PrUrl("8")), PrUrl("8"))]);
    }

    [Fact]
    public async Task TicketConversation_EntriesGroupedByAttemptInRunIdOrder()
    {
        await _h.AddAsync(Attempt("r-1", 0), Attempt("r-2", 60));
        Thread(Comment("c-3", "carol", 70, "Again."), Comment("c-1", "carol", -10, "Please."), Comment("c-2", "carol", 10, "Also."));

        var view = await ReadAsync();

        view.Attempts.Select(a => (a.RunId, a.Number)).Should().Equal(("r-1", 1), ("r-2", 2));
        view.Entries.Select(e => (e.Id, e.AttemptRunId, e.Group)).Should().Equal(
            ("ticket:c-1", null, TicketConversationGroups.Before), ("ticket:c-2", "r-1", null), ("ticket:c-3", "r-2", null));
    }

    [Fact]
    public async Task TicketConversation_CommentDuringPark_StaysWithTheParkedAttempt()
    {
        var parked = Attempt("r-1", 0);
        parked.StartedAt = T.AddMinutes(50); // the resume overwrote the start; the acts-read anchor is kept
        await _h.AddAsync(parked, Attempt("r-2", 100));
        Thread(Comment("c-1", "carol", 20, "Use main."));

        (await ReadAsync()).Entries.Single().AttemptRunId.Should().Be("r-1");
    }

    [Fact]
    public async Task TicketConversation_RefetchWithinAMinute_ReadsTheTrackerOnce()
    {
        await _h.AddAsync(Attempt("r-1", 0));

        await ReadAsync();
        await ReadAsync();

        _h.Tracker.Verify(t => t.GetCommentsAsync(It.IsAny<TicketId>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TicketConversation_HandbackRewrittenByTracker_FoldsByMarker()
    {
        await _h.AddAsync(Attempt("r-1", 0, status: "waiting_for_input"), Record("7"),
            Row("r-1", 4, RunConversationKinds.HandBack, 20, "## Agent Smith — the ticket reads two ways, and the work differs"),
            Note("7", "c-8", "## Agent Smith — the ticket reads two ways, and the work differs", 21, author: "agent-smith", ours: true));
        Thread(Comment("c-9", "agent-smith", 21, "<h2>Agent Smith &#8212; the ticket reads two ways, and the work differs</h2>"));

        var entries = (await ReadAsync()).Entries;

        entries.Should().ContainSingle().Which.Kind.Should().Be(RunConversationKinds.HandBack,
            "the copies posted on the ticket and the pull request are the recorded hand-back");
    }

    [Fact]
    public async Task TicketConversation_RecutAttempt_NamesTheDemandingComment()
    {
        await _h.AddAsync(Attempt("r-1", 0), Attempt("r-2", 60), Demanded("r-2", 30));
        Thread(Comment("c-7", "alice", 30, "cut this specification again"), Comment("c-6", "bob", 30, "me too"));

        (await ReadAsync()).Attempts.Single(a => a.RunId == "r-2").DemandEntryId.Should().Be("ticket:c-7");
    }

    [Fact]
    public async Task TicketConversation_RecutDemandOnPr_NamesThePrEntry()
    {
        await _h.AddAsync(Attempt("r-1", 0), Attempt("r-2", 60), Demanded("r-2", 30), Record("7"),
            Note("7", "n-4", "cut this specification again", 30, author: "alice"));

        (await ReadAsync()).Attempts.Single(a => a.RunId == "r-2").DemandEntryId.Should().Be($"pr-review:{PrKey.Of(PrUrl("7"))}:n-4");
    }

    private static RunAttempt Demanded(string runId, int minutes) => new()
    {
        RunId = runId, RevisionNumber = 2, RevisionCause = AgentSmith.Application.Services.Specs.SpecRevisionCause.RecutDemand,
        ApprovalAt = T.AddMinutes(minutes), ApprovalConversation = string.Empty, ApprovalPrincipal = "alice",
    };
}
