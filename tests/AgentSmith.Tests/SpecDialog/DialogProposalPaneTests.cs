using AgentSmith.Tests.TestSupport;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-15-6d9c: what the dashboard's second pane is given. The turn's typed outcome is
/// published from the outcome flow — which already receives it — before the person is
/// asked to approve it, and what the filing attempt actually created is published from the
/// sink that holds the report. Both reach the one session group and nothing wider.
/// </summary>
public sealed class DialogProposalPaneTests : IDisposable
{
    private const string Dialog = "d-6d9c";

    private readonly RecordingDialogHub _hub = new();
    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;

    public DialogProposalPaneTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        MigratedStoreTemplate.CopyInto(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Flow_WithASpecProposal_PublishesTheTypedProposal()
    {
        var flow = Flow();

        await flow.HandleAsync(await OpenAsync(), new SpecOutcome(Draft("p9001")), CancellationToken.None);

        var push = Proposals().Single();
        push.ProposalId.Should().NotBeNullOrEmpty("2026-11-10-ccd9: the shown proposal carries the id a decision names");
        push.Kind.Should().Be(SpecDialogProposalComposer.SpecKind);
        push.Spec!.SpecId.Should().Be("p9001");
        push.Spec.Goal.Should().Be("goal of p9001");
        push.Spec.Steps.Should().Equal("open the seam");
        push.Spec.Tests.Should().Equal("Flow_Scenario_Expected");
        push.Spec.Done.Should().Equal("the pane shows it");
        // 2026-10-02-3f06c: and what it rests on.
        push.Spec.Facts.Should().Equal(new SpecFact("the seam exists", "repo/src/Seam.cs:3"));
        push.Spec.Assumptions.Should().Equal("nobody calls it yet");
    }

    [Fact]
    public async Task Flow_WithAnAnswerOutcome_PublishesNoProposal()
    {
        var flow = Flow();

        await flow.HandleAsync(await OpenAsync(), new AnswerOutcome(), CancellationToken.None);

        Proposals().Should().BeEmpty(
            "an answer proposes nothing, so the pane keeps whatever is still under discussion");
    }

    [Fact]
    public async Task Flow_ASecondProposal_SupersedesTheFirstUnderANewId()
    {
        var flow = Flow();
        var state = await OpenAsync();

        await flow.HandleAsync(state, new SpecOutcome(Draft("p9001")), CancellationToken.None);
        await flow.HandleAsync(state, new SpecOutcome(Draft("p9002")), CancellationToken.None);

        Proposals().Select(push => push.Spec!.SpecId).Should().Equal(["p9001", "p9002"],
            "a later turn's proposal takes the place of the one awaiting a decision");
        Proposals().Select(push => push.ProposalId).Distinct().Should().HaveCount(2, "each awaiting proposal has its own id");
    }

    [Fact]
    public async Task Proposal_ReachesOnlyTheSessionGroup()
    {
        var flow = Flow();

        await flow.HandleAsync(await OpenAsync(), new SpecOutcome(Draft("p9001")), CancellationToken.None);

        _hub.Pushes.Should().OnlyContain(push => push.Group == HubGroups.SpecDialog(Dialog),
            "a design conversation is addressed to the one person holding it");
    }

    [Fact]
    public async Task Propose_OnAChatPlatform_PublishesNothingToTheHub()
    {
        var channel = Channel();

        await channel.ProposeAsync(
            State() with { Platform = "slack" }, new SpecOutcome(Draft("p9001")),
            CancellationToken.None);

        _hub.Pushes.Should().BeEmpty(
            "a Slack thread reads its proposal in the thread, and its id is a dialog id "
            + "nobody has joined");
    }

    [Fact]
    public void Compose_AnEpic_OrdersTheChildrenTheWayTheFilerWillFileThem()
    {
        var epic = new EpicOutcome(
            Draft("p9000"), [Draft("p9000a", requires: ["p9000b"]), Draft("p9000b")]);

        var push = Composer().Compose(Dialog, epic, DateTimeOffset.UnixEpoch)!;

        push.Kind.Should().Be(SpecDialogProposalComposer.EpicKind);
        push.Parent!.SpecId.Should().Be("p9000");
        push.Children.Select(child => child.SpecId).Should().Equal(["p9000b", "p9000a"],
            "the cut listed them the other way round; the filer follows the edges, and a "
            + "pane listing them differently shows a plan that is not the one about to be created");
    }

    [Fact]
    public void Compose_ABugOutcome_CarriesTheBodyTheFilerWouldFile()
    {
        var bug = new BugOutcome(new BugTicketDraft("Widget drops", "It drops.", "It stops dropping."));

        var push = Composer().Compose(Dialog, bug, DateTimeOffset.UnixEpoch)!;

        push.Kind.Should().Be(SpecDialogProposalComposer.BugKind);
        push.Bug!.Title.Should().Be("Widget drops");
        push.Bug.Body.Should().Be(new BugTicketRenderer().RenderBody(
            new BugTicketDraft("Widget drops", "It drops.", "It stops dropping.")));
    }

    [Fact]
    public void Compose_AnAnswerOutcome_ComposesNothing() =>
        Composer().Compose(Dialog, new AnswerOutcome(), DateTimeOffset.UnixEpoch).Should().BeNull();

    [Fact]
    public async Task Filed_AfterAPartialFailure_PublishesWhatWasCreatedAndTheError()
    {
        var report = new FilingReport(
            [new FiledTicket("https://tracker/1", "p9000: the cut")], "the tracker refused the child");

        await Channel().FiledAsync(State(), report, CancellationToken.None);

        var push = Filings().Single();
        push.Filed.Should().Equal([new FiledTicket("https://tracker/1", "p9000: the cut")],
            "a partial epic must never silently lose the children it did create");
        push.Error.Should().Be("the tracker refused the child");
    }

    /// <summary>2026-09-17-042ea: a child the tracker would not link is said, not hidden.</summary>
    [Fact]
    public async Task Filed_WithANote_PublishesTheNoteAndNamesItInTheNotice()
    {
        var report = new FilingReport([new FiledTicket("https://tracker/2", "p9000a: slice")], Error: null)
        {
            Notes = ["https://tracker/2 is not linked to its parent https://tracker/1: refused"],
        };

        await Channel().FiledAsync(State(), report, CancellationToken.None);

        Filings().Single().Notes.Should().Equal(report.Notes);
        new SpecDialogOutcomeComposer().ComposeFiled(new SpecOutcome(new SpecDraft("p9000a", "slice", "spec: p9000a", [])), report)
            .In(SpecDialogMarkup.For("slack")).Should().Contain("is not linked to its parent");
    }

    [Fact]
    public async Task Filed_AfterASuccessfulFiling_PublishesTheReferencesAndTitles()
    {
        var report = new FilingReport(
            [new FiledTicket("https://tracker/7", "p9001: the spec")], Error: null);

        await Channel().FiledAsync(State(), report, CancellationToken.None);

        var push = Filings().Single();
        push.Error.Should().BeNull();
        push.Filed.Single().Reference.Should().Be("https://tracker/7");
        push.Filed.Single().Title.Should().Be("p9001: the spec");
    }

    [Fact]
    public void RenderBody_WithAcceptanceCriteria_PutsThemUnderTheirOwnHeading() =>
        new BugTicketRenderer()
            .RenderBody(new BugTicketDraft("t", "It drops.", "It stops dropping."))
            .Should().Be("It drops.\n\n## Acceptance criteria\n- It stops dropping.",
                "2026-10-01-f5c3b: an unlisted line is not an item, and only an item is read back");

    [Fact]
    public void RenderBody_WithoutAcceptanceCriteria_IsTheDescriptionAlone() =>
        new BugTicketRenderer()
            .RenderBody(new BugTicketDraft("t", "It drops.", null))
            .Should().Be("It drops.");

    private IEnumerable<SpecDialogProposalPush> Proposals() =>
        _hub.Pushes.Where(push => push.Method == "SpecDialogProposal")
            .Select(push => (SpecDialogProposalPush)push.Args[0]!);

    private IEnumerable<SpecDialogFilingPush> Filings() =>
        _hub.Pushes.Where(push => push.Method == "SpecDialogFiled")
            .Select(push => (SpecDialogFilingPush)push.Args[0]!);

    private static SpecDialogProposalComposer Composer() =>
        new(new EpicChildOrderer(), new BugTicketRenderer());

    private DashboardOutcomeChannel Channel() =>
        new(Composer(), NullLogger<DashboardOutcomeChannel>.Instance, _hub);

    /// <summary>2026-11-10-ccd9: the real flow over this test's store; nothing waits on an answer.</summary>
    private SpecDialogOutcomeFlow Flow() =>
        new SpecDialogDecisionHarness(_context, Channel(), Mock.Of<IOutcomeSink>()).Flow();

    private Task<ConversationState> OpenAsync() =>
        new SpecDialogDecisionHarness(_context, Channel(), Mock.Of<IOutcomeSink>()).Sessions.OpenAsync(
            "dashboard", Dialog, Dialog, "U1", new ActiveScope { Project = "sample", Repos = ["repo-a"] }, CancellationToken.None);

    private static ConversationState State() => new()
    {
        JobId = "job-1",
        ChannelId = Dialog,
        ThreadId = Dialog,
        UserId = "U1",
        Platform = "dashboard",
        Project = "sample",
        TicketId = string.Empty,
        StartedAt = DateTimeOffset.UtcNow,
        Mode = ConversationMode.SpecDialog,
    };

    private static SpecDraft Draft(string specId, IReadOnlyList<string>? requires = null) =>
        new(specId, $"goal of {specId}", $"spec: {specId}", requires ?? [])
        {
            Steps = [new SpecStep("seam", "open the seam", null)],
            Tests = ["Flow_Scenario_Expected"],
            Done = ["the pane shows it"],
            Facts = [new SpecFact("the seam exists", "repo/src/Seam.cs:3")],
            Assumptions = ["nobody calls it yet"],
        };
}
