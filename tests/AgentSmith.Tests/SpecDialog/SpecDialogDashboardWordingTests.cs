using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-17-042ek: what the framework's own lines say to a reader who has no thread and no
/// slash command. The dialog PAGE is such a reader — it has a box, a button per decision and a
/// conversation list — so a line telling it to "reply in this thread" or to type "/spec new"
/// names two affordances it does not have, and the operator is left looking for them.
/// <para>
/// The chat assertions are here for the same reason: the selector is one expression per
/// sentence, so a wording moved to the wrong side of it is invisible unless both sides are read.
/// </para>
/// </summary>
public sealed class SpecDialogDashboardWordingTests
{
    private const string Command = "/spec";
    private const string Thread = "thread";

    private readonly SpecDialogReplyComposer _replies = new();
    private readonly SpecDialogOutcomeComposer _outcomes = new();

    [Fact]
    public void SpecDialogReply_Opened_OnTheDashboard_NamesNoCommandOrThread()
    {
        var opened = _replies.ComposeOpened(State()).In(SpecDialogMarkup.CommonMark);

        opened.Should().NotContain(Command).And.NotContain(Thread);
        opened.Should().Contain("New conversation",
            "the page's own affordance replaces the command it cannot type");
        opened.Should().Contain("**sample**", "the dialect is still the browser's");
    }

    [Fact]
    public void SpecDialogReply_Opened_OnSlack_IsUnchanged()
    {
        var opened = _replies.ComposeOpened(State()).In(SpecDialogMarkup.ChatMrkdwn);

        opened.Should().Be(
            "Spec dialog `s-1` opened — scope *sample* (repo-a). Describe what you want to "
            + "build; `/spec new` forks this thread onto a fresh session.");
    }

    [Fact]
    public void SpecDialogReply_ChoiceRequired_OnTheDashboard_NamesNoCommand()
    {
        var choice = _replies.ComposeChoiceRequired(["alpha", "beta"]);

        var page = choice.In(SpecDialogMarkup.CommonMark);
        page.Should().NotContain(Command);
        page.Should().Contain("**alpha**").And.Contain("**beta**",
            "the scopes are still named — only the way to pick one changes");
        page.Should().Contain("Project list",
            "the control that picks one is labelled Project, not 'project picker'");

        choice.In(SpecDialogMarkup.ChatMrkdwn).Should().Contain("`/spec alpha`");
    }

    // Found by review: "listed beside this one" named a conversation that is not there. Opening
    // one mints a fresh dialog id and clears the view first (useSpecDialog switchTo), so the
    // page this reply lands on holds nothing — only the list does.
    [Fact]
    public void SpecDialogReply_SessionNotFound_OnTheDashboard_PointsAtTheConversationList()
    {
        var notFound = _replies.ComposeSessionNotFound("s-9");

        var page = notFound.In(SpecDialogMarkup.CommonMark);
        page.Should().NotContain(Command).And.Contain("on the left");
        page.Should().NotContain("this one",
            "the conversation this reply lands on was cleared before it was sent");

        notFound.In(SpecDialogMarkup.ChatMrkdwn).Should().Contain("`/spec list`");
    }

    [Fact]
    public void SpecDialogReply_FilingFailure_OnTheDashboard_NamesNoThread()
    {
        var report = new FilingReport([new FiledTicket("T-1", "the parent")], "the tracker refused");

        var page = _outcomes.ComposeFilingFailure(report).In(SpecDialogMarkup.CommonMark);

        page.Should().NotContain(Thread).And.NotContain(Command);
        page.Should().Contain("T-1", "a partial filing still names every ticket that exists");
        page.Should().Contain("the tracker refused");
        _outcomes.ComposeFilingFailure(report).In(SpecDialogMarkup.ChatMrkdwn)
            .Should().Contain("in this thread");
    }

    // Case-insensitively, and on the bare word: a sentence opening with "Thread" or naming
    // "threaded" is the same mistake, and a sweep that only sees one spelling of it invites the
    // other. FluentAssertions' EquivalentOf comparison is the case-insensitive one.
    [Fact]
    public void SpecDialogReplies_EveryPageReachableSentence_OnTheDashboard_NamesNoCommandOrThread()
    {
        foreach (var (name, reply) in PageReachable())
        {
            var text = reply.In(SpecDialogMarkup.CommonMark);
            text.Should().NotContainEquivalentOf(
                Command, $"{name} is reachable from a page with no commands");
            text.Should().NotContainEquivalentOf(
                Thread, $"{name} is reachable from a page with no threads");
        }
    }

    // 2026-09-17-042ek, found by review: the page posts what the operator typed, so these two
    // are reachable from it — and answering "what is /spec resume?" with "/spec list" on a
    // surface that has neither is the one thing this phase exists to stop.
    [Fact]
    public void SpecDialogReply_TypedCommandReplies_OnTheDashboard_NameNoCommand()
    {
        var usage = _replies.ComposeResumeUsage();
        var empty = _replies.ComposeList([]);

        usage.In(SpecDialogMarkup.CommonMark).Should().NotContain(Command)
            .And.Contain("on the left", "the conversation list is where one is opened");
        empty.In(SpecDialogMarkup.CommonMark).Should().NotContain(Command)
            .And.Contain("New conversation");

        usage.In(SpecDialogMarkup.ChatMrkdwn).Should().Contain("`/spec resume <id>`");
        empty.In(SpecDialogMarkup.ChatMrkdwn).Should().Contain("`/spec`");
    }

    [Fact]
    public void OutcomeConfirmation_BoundForTheDashboard_IsEmpty()
    {
        var confirmation = _outcomes.ComposeConfirmation(Epic()).In(SpecDialogMarkup.CommonMark);

        confirmation.Should().BeEmpty(
            "the approval card renders the summary, the findings and the two buttons itself");
    }

    [Fact]
    public void OutcomeConfirmation_BoundForSlack_ListsTheSlices()
    {
        var confirmation = _outcomes.ComposeConfirmation(Epic()).In(SpecDialogMarkup.ChatMrkdwn);

        confirmation.Should().Contain("*epic*").And.Contain("`p9000a`").And.Contain("`p9000b`");
        confirmation.Should().Contain("Approve to file this outcome",
            "a chat thread has no card to carry the sentence for it");
    }

    /// <summary>
    /// 2026-09-17-042ek: where 2026-09-17-042ed's findings survive. On a page they reach the
    /// operator on the proposal push, which the approval card lists them from; in chat there is
    /// no card, so the confirmation text is the only place the objection can appear.
    /// </summary>
    [Fact]
    public void OutcomeConfirmation_BoundForSlack_ListsTheReviewsFindings()
    {
        var reviewed = Epic() with
        {
            Findings =
            [
                new ProposalFinding(
                    "p9000a", "false premise", "the endpoint is already there", Quote: null,
                    Evidence: "[P2] repo-a: the proposal review ran 'read src/Api.cs' exited 0"),
            ],
        };

        var confirmation = _outcomes.ComposeConfirmation(reviewed);

        confirmation.In(SpecDialogMarkup.ChatMrkdwn)
            .Should().Contain("The review of this proposal found:")
            .And.Contain("false premise: the endpoint is already there")
            .And.Contain("[P2] repo-a: the proposal review ran 'read src/Api.cs' exited 0");
        confirmation.In(SpecDialogMarkup.CommonMark).Should().BeEmpty(
            "the card lists them from the push instead");
    }

    /// <summary>
    /// Every framework line the dialog page can be sent — the two that answer a TYPED command
    /// included. The page posts the operator's text verbatim (useSpecDialog send → the ingest
    /// endpoint → SpecDialogRouter → SpecCommandParser), so typing "/spec list" into the box
    /// reaches the session list and "/spec resume" with no id reaches the resume usage. An
    /// earlier draft of this phase carved those two out as unreachable, which was read off what
    /// the page posts on the operator's BEHALF rather than off what it posts.
    /// </summary>
    private IEnumerable<(string Name, ComposedReply Reply)> PageReachable() =>
    [
        ("opened", _replies.ComposeOpened(State())),
        ("resumed", _replies.ComposeResumed(State())),
        ("already open", _replies.ComposeAlreadyOpen(State())),
        ("resume refused, question pending",
            _replies.ComposeResumeRefused(new SpecDialogResumeRefused("s-1", QuestionPending: true))),
        ("resume refused, turn running",
            _replies.ComposeResumeRefused(new SpecDialogResumeRefused("s-1", QuestionPending: false))),
        ("session not found", _replies.ComposeSessionNotFound("s-9")),
        ("resume usage", _replies.ComposeResumeUsage()),
        ("no open sessions", _replies.ComposeList([])),
        ("open sessions", _replies.ComposeList([State()])),
        ("choice required", _replies.ComposeChoiceRequired(["alpha", "beta"])),
        ("unknown project", _replies.ComposeUnknownProject("gamma", ["alpha"])),
        ("turn in progress", _replies.ComposeTurnInProgress(State())),
        ("turn failed", _replies.ComposeTurnFailed("the model timed out")),
        ("question", _replies.ComposeQuestion("Which repository holds the ledger?")),
        ("confirmation", _outcomes.ComposeConfirmation(Epic())),
        ("rejected", _outcomes.ComposeRejected()),
        ("timed out", _outcomes.ComposeTimeout()),
        ("edit acknowledged", _outcomes.ComposeEditAck("make it two slices")),
        ("filed", _outcomes.ComposeFiled(Epic(), new FilingReport([new FiledTicket("T-1", "t")], null))),
        ("filing failed", _outcomes.ComposeFilingFailure(new FilingReport([], "the tracker refused"))),
    ];

    private static EpicOutcome Epic() => new(
        Draft("p9000", "the parent goal"),
        [Draft("p9000a", "the first slice"), Draft("p9000b", "the second slice")]);

    private static PhaseDraft Draft(string phaseId, string goal) =>
        new(phaseId, goal, $"phase: {phaseId}", []);

    private static ConversationState State() => new()
    {
        JobId = "s-1", ChannelId = "d-1", UserId = "person-a",
        Platform = "dashboard", Project = "sample",
        TicketId = string.Empty, StartedAt = DateTimeOffset.UtcNow,
        Scope = new ActiveScope { Project = "sample", Repos = ["repo-a"] },
    };
}
