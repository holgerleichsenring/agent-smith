using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-17-042eg: approving files the work AND starts it — but only what would actually be
/// claimed. Every ticket on the report says what it became and why: started, not started with the
/// thing that is in the way, or a record, which is not work at all.
/// </summary>
public sealed class FiledWorkStartTests
{
    /// <summary>
    /// 2026-09-22-766b: the one label a filing writes, so it is the tag a project can resolve by
    /// that the ticket ALREADY carries — which is what these cases are about. It used to be the
    /// phase word; no filing writes that any more.
    /// </summary>
    private const string ByTag = FiledTicketLabels.ApprovedSetStamp;

    [Fact]
    public async Task WorkTicket_ProjectResolvingByATagTheTicketCarries_ReportsStarted()
    {
        var provider = new StartProvider("To Do");

        var report = await FileAsync(provider, Phase(), Routing(ByTag, "To Do"), mayStartRuns: true);

        Work(report).Start!.State.Should().Be(FiledStartState.Started,
            "the phase label IS the resolution tag, so the poller's own envelope routes it");
        provider.Moves.Should().BeEmpty("it was created in a status that already triggers");
    }

    [Fact]
    public async Task WorkTicket_ProjectResolvingByAreaPath_ReportsNotStartedNamingTheMissingTag()
    {
        var provider = new StartProvider("To Do");

        var report = await FileAsync(
            provider, Phase(), Routing(null, "To Do", ResolutionStrategy.AreaPath, @"Area\Widgets"),
            mayStartRuns: true);

        var start = Work(report).Start!;
        start.State.Should().Be(FiledStartState.NotStarted);
        start.Reason.Should().Contain("AreaPath").And.Contain(@"Area\Widgets")
            .And.Contain("webhook", "the webhook path resolves more than the poller can");
        provider.Moves.Should().BeEmpty();
    }

    /// <summary>
    /// 2026-09-20-2ba8: a tag the project resolves by is now PUT ON the ticket, so "a tag it does
    /// not carry" is no longer a way to make a filed ticket unresolvable. What still is: a
    /// resolution no label can satisfy, because a polled envelope carries labels, ticket id and
    /// platform and nothing else. That is the premise these two now stand on.
    /// </summary>
    [Fact]
    public async Task WorkTicket_InTriggerStatusButUnresolvable_ReportsNotStarted()
    {
        var provider = new StartProvider("To Do");

        var report = await FileAsync(
            provider, Phase(), Routing(null, "To Do", ResolutionStrategy.AreaPath, @"Area\Widgets"),
            mayStartRuns: true);

        Work(report).Start!.State.Should().Be(FiledStartState.NotStarted,
            "sitting in a trigger status is not being started when no project claims it");
        provider.Labels.Should().BeEmpty("no label can put an area path on a polled envelope");
    }

    [Fact]
    public async Task WorkTicket_OutsideTheTriggerStatusesAndUnresolvable_IsNotMoved()
    {
        var provider = new StartProvider("New");

        await FileAsync(
            provider, Phase(), Routing(null, "To Do", ResolutionStrategy.AreaPath, @"Area\Widgets"),
            mayStartRuns: true);

        provider.Moves.Should().BeEmpty(
            "moving it would leave it in a trigger status no poll ever claims");
    }

    [Fact]
    public async Task WorkTicket_TriggerDisabledByAStartupFinding_ReportsNotStarted()
    {
        var provider = new StartProvider("New");
        var findings = new AgentSmith.Infrastructure.Core.Services.Configuration.StartupFindings();
        findings.Record(new StartupFinding(
            "tracker", StartupFindingSeverity.Blocking, "the tracker has no credentials",
            Project: "proj", Trigger: TriggerKinds.AzureDevOps));

        var report = await FileAsync(
            provider, Phase(), Routing(ByTag, "To Do"), mayStartRuns: true, findings: findings);

        var start = Work(report).Start!;
        start.State.Should().Be(FiledStartState.NotStarted,
            "a trigger a blocking finding disabled would spawn a run that cannot finish");
        start.Reason.Should().Contain("the tracker has no credentials")
            .And.Contain("startup finding")
            .And.NotContain("does not carry",
                "the ticket carries the tag; telling this operator to add one is a WRONG answer");
        provider.Moves.Should().BeEmpty();
    }

    [Fact]
    public async Task WorkTicket_TriggerWithNoProjectResolution_SaysTheResolutionIsMissing()
    {
        var provider = new StartProvider("New");

        var report = await FileAsync(
            provider, Phase(), Routing(ByTag, "To Do", noResolution: true), mayStartRuns: true);

        Work(report).Start!.Reason.Should().Contain("project_resolution")
            .And.NotContain("Tag '", "there is no resolution to name a tag from");
    }

    [Fact]
    public async Task WorkTicket_TrackerWithNoTriggerAtAll_SaysSo()
    {
        var provider = new StartProvider("New");

        var report = await FileAsync(
            provider, Phase(), Routing(ByTag, "To Do", noTrigger: true), mayStartRuns: true);

        Work(report).Start!.Reason.Should().Contain("declares no azuredevops trigger at all");
    }

    /// <summary>
    /// ProjectResolver is ambiguous by design and TrackerPoller spawns for every match, so a
    /// ticket that resolves elsewhere IS routed — just not as the work this conversation filed.
    /// "Nothing would route it" would be false, and the operator would go looking for a tag.
    /// <para>
    /// 2026-09-20-2ba8: the filing project resolves by an AREA PATH now. Its own tag would be put
    /// on the ticket and it would match itself, so the only way it loses to a neighbour is a
    /// resolution a label cannot satisfy — and the neighbour's tag is one the ticket already
    /// carries, exactly as a hand-tagged ticket would.
    /// </para>
    /// </summary>
    [Fact]
    public async Task WorkTicket_ResolvingOnlyToAnotherProject_NamesThatProject()
    {
        var provider = new StartProvider("New");

        var report = await FileAsync(
            provider, Phase(),
            Routing(null, "To Do", ResolutionStrategy.AreaPath, @"Area\Widgets", otherProjectTag: ByTag),
            mayStartRuns: true);

        var start = Work(report).Start!;
        start.State.Should().Be(FiledStartState.NotStarted);
        start.Reason.Should().Contain("'neighbour'").And.Contain("spawn a run for it");
        provider.Moves.Should().BeEmpty("the filing project is not the one that would claim it");
    }

    /// <summary>
    /// The third silent drop: the resolution matched and the project's own pipeline_from_label did
    /// not. Reached directly, with the tag already among the labels — which is what the starter
    /// hands its own resolution once the tagging has landed.
    /// <para>
    /// 2026-09-20-2ba8: the sentence that used to stand here — that nothing the filer creates can
    /// carry an operator label — is false now, and the filer-level case it excused is pinned by
    /// <see cref="FiledWorkStart_ABugOnAProjectWithAPipelineMapThatIgnoresTheTag_IsStillNotRouted"/>.
    /// A tag the ticket already carries is not written twice, so this call still writes nothing.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Ticket_CarryingTheTagButNoPipelineRule_SaysThePipelineRulesDroppedIt()
    {
        var provider = new StartProvider("New");
        var config = Routing(
            "operator-tag", "To Do",
            pipelineFromLabel: new Dictionary<string, string> { ["something-else"] = "code" });
        var filed = new List<FiledTicket> { new("https://tracker.test/1", "t") { TicketId = "1" } };

        await FiledWorkDoubles.Starter(config).StampAsync(
            provider, config.Projects["proj"], new CreatedTicket(new TicketId("1"), null),
            ["operator-tag"], mayStartRuns: true, filed, CancellationToken.None);

        filed[0].Start!.Reason.Should().Contain("pipeline_from_label")
            .And.Contain("carries the tag 'operator-tag'");
        provider.Moves.Should().BeEmpty();
        provider.Labels.Should().BeEmpty("the ticket already carries the tag");
    }

    // ---- 2026-09-20-2ba8: the routing tag, put on just before the resolution reads it ----

    [Fact]
    public async Task FiledWorkStart_AProjectResolvingByTag_TagsTheTicketAndReportsItRouted()
    {
        var provider = new StartProvider("To Do");

        var report = await FileAsync(
            provider, Phase(), Routing("operator-tag", "To Do"), mayStartRuns: true);

        provider.Labels.Should().Equal([("1", "operator-tag")],
            "the filers stamp framework labels only; the value the project resolves by is this");
        var start = Work(report).Start!;
        start.State.Should().Be(FiledStartState.Started);
        start.Reason.Should().StartWith("the tag 'operator-tag' was added to it; ");
    }

    /// <summary>
    /// The ORDER is the phase: a resolution run before the tag is written answers what the poller
    /// would have answered yesterday, and the ticket is reported not started while carrying the
    /// very tag that routes it.
    /// </summary>
    [Fact]
    public async Task FiledWorkStart_AProjectResolvingByTag_TagsBeforeItResolves()
    {
        var provider = new StartProvider("To Do");
        var config = Routing("operator-tag", "To Do");
        var resolver = new LabelWatchingResolver(FiledWorkDoubles.Resolver(), () => provider.Labels.Count);

        await FileAsync(provider, Phase(), config, mayStartRuns: true, resolver: resolver);

        resolver.LabelsWhenResolved.Should().Equal([1],
            "the tag is on the ticket before the envelope it is resolved from is built");
    }

    /// <summary>
    /// The value defaults to empty and only the config-studio write path refuses an empty one, so
    /// a file-based configuration can carry it. An empty label on a customer's tracker is junk
    /// nobody asked for, and it would route nothing anyway.
    /// </summary>
    [Fact]
    public async Task FiledWorkStart_AnEmptyResolutionValue_IsNeverWrittenToTheTracker()
    {
        var provider = new StartProvider("To Do");

        var report = await FileAsync(provider, Phase(), Routing("", "To Do"), mayStartRuns: true);

        provider.Labels.Should().BeEmpty();
        var start = Work(report).Start!;
        start.State.Should().Be(FiledStartState.NotStarted);
        start.Reason.Should().Contain("its value is empty").And.Contain("project_resolution.value");
    }

    /// <summary>
    /// A create or an update the tracker REFUSES would cost the filing itself, turning "filed but
    /// not started" into "nothing was filed" — strictly worse than the defect being fixed. So a
    /// value the tracker's own label grammar cannot carry is skipped and named.
    /// </summary>
    [Theory]
    [InlineData(TrackerType.GitLab, "widgets,api", "a comma")]
    [InlineData(TrackerType.AzureDevOps, "widgets;api", "a semicolon")]
    [InlineData(TrackerType.Jira, "widget team", "whitespace")]
    public async Task FiledWorkStart_ATagTheTrackerCannotCarry_IsSkippedAndReported(
        TrackerType tracker, string tag, string what)
    {
        var provider = new StartProvider("To Do");

        var report = await FileAsync(
            provider, Phase(), Routing(tag, "To Do", tracker: tracker), mayStartRuns: true);

        provider.Labels.Should().BeEmpty("a refused write costs the filing, not just the start");
        var start = Work(report).Start!;
        start.State.Should().Be(FiledStartState.NotStarted);
        start.Reason.Should().Contain($"the tag '{tag}' was NOT written").And.Contain(what);
    }

    [Fact]
    public async Task FiledWorkStart_ATaggingCallThatThrows_StillReportsTheTicket()
    {
        var provider = new StartProvider("To Do")
        {
            LabelError = new InvalidOperationException("the tracker refused the label"),
        };

        var report = await FileAsync(
            provider, Phase(), Routing("operator-tag", "To Do"), mayStartRuns: true);

        report.Error.Should().BeNull("the ticket exists; a tag that failed is a reason, not a failure");
        var start = Work(report).Start!;
        start.State.Should().Be(FiledStartState.NotStarted, "the tag is not on the ticket");
        start.Reason.Should().Contain("the tracker refused the label")
            .And.Contain("everything else was filed anyway");
    }

    /// <summary>
    /// A tracker the port's default answers for: the tag was not written, so it must not join the
    /// envelope either — a resolution that matched on a label nobody wrote would move the ticket
    /// into a trigger status it then sits in forever, reported as started.
    /// </summary>
    [Fact]
    public async Task FiledWorkStart_ATrackerThatCannotLabel_ReportsTheTagAsNotAdded()
    {
        var provider = new StartProvider("New") { CanLabel = false };

        var report = await FileAsync(
            provider, Phase(), Routing("operator-tag", "To Do"), mayStartRuns: true);

        provider.Moves.Should().BeEmpty("nothing routes a ticket that never got the tag");
        var start = Work(report).Start!;
        start.State.Should().Be(FiledStartState.NotStarted);
        start.Reason.Should().Contain("the tag 'operator-tag' was not added")
            .And.Contain("added by hand");
    }

    [Fact]
    public async Task FiledWorkStart_AProjectResolvingByAreaPath_TagsNothing()
    {
        var provider = new StartProvider("To Do");

        await FileAsync(
            provider, Phase(), Routing(null, "To Do", ResolutionStrategy.AreaPath, @"Area\Widgets"),
            mayStartRuns: true);

        provider.Labels.Should().BeEmpty("an area path is not something a label can carry");
    }

    [Fact]
    public async Task FiledWorkStart_AProjectWithNoProjectResolution_TagsNothing()
    {
        var provider = new StartProvider("To Do");

        await FileAsync(
            provider, Phase(), Routing(ByTag, "To Do", noResolution: true), mayStartRuns: true);

        provider.Labels.Should().BeEmpty("there is no resolution to read a tag out of");
    }

    [Fact]
    public async Task FiledWorkStart_ATagTheTicketAlreadyCarries_IsNotAddedTwice()
    {
        var provider = new StartProvider("To Do");

        var report = await FileAsync(provider, Phase(), Routing(ByTag, "To Do"), mayStartRuns: true);

        provider.Labels.Should().BeEmpty("the approval stamp IS the tag this project resolves by");
        Work(report).Start!.State.Should().Be(FiledStartState.Started);
    }

    /// <summary>
    /// A bug is filed with no framework label deliberately, so the project's own rules decide which
    /// pipeline claims it. The routing tag binds no pipeline: it is what makes the project claim
    /// the ticket at all, and a declared pipeline_from_label that does not name it still drops it.
    /// </summary>
    [Fact]
    public async Task FiledWorkStart_ABugOnAProjectWithAPipelineMapThatIgnoresTheTag_IsStillNotRouted()
    {
        var provider = new StartProvider("New");
        var config = Routing(
            "operator-tag", "To Do",
            pipelineFromLabel: new Dictionary<string, string> { ["something-else"] = "code" });

        var report = await FileAsync(provider, Bug(), config, mayStartRuns: true);

        provider.Labels.Should().Equal([("1", "operator-tag")], "a bug is tagged like any other work");
        var start = report.Filed.Should().ContainSingle().Subject.Start!;
        start.State.Should().Be(FiledStartState.NotStarted);
        start.Reason.Should().Contain("pipeline_from_label");
        provider.Moves.Should().BeEmpty();
    }

    /// <summary>
    /// 2026-09-22-b3d7: a cut files ONE ticket, so there is nothing beside the work ticket for a
    /// tag to reach. The starter is the only thing that tags, and it runs once per filing.
    /// </summary>
    [Fact]
    public async Task ApprovedCut_TagsItsOneWorkTicketAndNothingElse()
    {
        var provider = new StartProvider("New");

        var report = await FileAsync(
            provider, Epic(), Routing("operator-tag", "To Do"), mayStartRuns: true);

        report.Filed.Should().ContainSingle().Which.Start!.State.Should().Be(FiledStartState.Started);
        provider.Labels.Should().Equal([("1", "operator-tag")], "only the work ticket is ever tagged");
    }

    [Fact]
    public async Task Filing_ApprovedSetIsStoredBeforeTheTicketIsTagged()
    {
        var provider = new StartProvider("New");
        var store = new MoveWatchingStore(() => provider.Moves.Count, () => provider.Labels.Count);

        await FileAsync(provider, Epic(), Routing("operator-tag", "To Do"), mayStartRuns: true, store: store);

        store.LabelsWhenSaved.Should().Equal([0],
            "a tagged ticket with no stored set is claimed by the poller and derives its own spec");
        provider.Labels.Should().ContainSingle("the tag did land, after the set was stored");
    }

    [Fact]
    public async Task WorkTicket_ProjectWithoutTriggerStatuses_ReportsStartedAndMovesNothing()
    {
        var provider = new StartProvider("Anything At All");

        var report = await FileAsync(provider, Phase(), Routing(ByTag), mayStartRuns: true);

        Work(report).Start!.State.Should().Be(FiledStartState.Started,
            "TrackerPoller allows any status when the operator named none");
        provider.Moves.Should().BeEmpty();
    }

    [Fact]
    public async Task WorkTicket_OutsideTheTriggerStatusesWithRunsControl_IsMovedToTheFirstOne()
    {
        var provider = new StartProvider("New");

        var report = await FileAsync(
            provider, Phase(), Routing(ByTag, "To Do", secondStatus: "Ready"), mayStartRuns: true);

        provider.Moves.Should().Equal([("1", "To Do")], "the FIRST trigger status is the target");
        Work(report).Start!.State.Should().Be(FiledStartState.Started);
    }

    [Fact]
    public async Task WorkTicket_OutsideTheTriggerStatusesWithoutRunsControl_IsNotMovedAndWaits()
    {
        var provider = new StartProvider("New");

        var report = await FileAsync(provider, Phase(), Routing(ByTag, "To Do"), mayStartRuns: false);

        provider.Moves.Should().BeEmpty("the move is what starts a run, and that needs runs.control");
        var reason = Work(report).Start!.Reason;
        reason.Should().Contain("runs.control")
            .And.Contain("Move it there in the tracker",
                "the bool is read once, on the request that opened the turn — nothing re-reads it, "
                + "so a colleague being granted the role later ends no wait")
            .And.Contain("SECOND work ticket", "approving again is not the way back");
        reason.Should().NotContain("waiting for someone");
    }

    [Fact]
    public async Task WorkTicket_MoveReadsBackUnchanged_ReportsNotStartedWithTheHeldStatus()
    {
        var provider = new StartProvider("New") { MoveTakesEffect = false };

        var report = await FileAsync(provider, Phase(), Routing(ByTag, "To Do"), mayStartRuns: true);

        provider.Moves.Should().ContainSingle();
        var start = Work(report).Start!;
        start.State.Should().Be(FiledStartState.NotStarted);
        start.Reason.Should().Contain("New", "the status it is actually still in is named");
    }

    [Fact]
    public async Task WorkTicket_MoveThrows_ReportsNotStartedAndTheFilingContinues()
    {
        var provider = new StartProvider("New")
        {
            MoveError = new InvalidOperationException("the workflow refused it"),
        };

        var report = await FileAsync(provider, Phase(), Routing(ByTag, "To Do"), mayStartRuns: true);

        report.Error.Should().BeNull("the ticket exists; a move that failed is a reason, not a failure");
        Work(report).Start!.Reason.Should().Contain("the workflow refused it");
    }

    [Fact]
    public async Task WorkTicket_GitHubTriggerStatusNotNative_IsNotMovedAndGetsNoLabel()
    {
        var provider = new StartProvider("open");

        var report = await FileAsync(
            provider, Phase(), Routing(ByTag, "triage", tracker: TrackerType.GitHub),
            mayStartRuns: true);

        provider.Moves.Should().BeEmpty(
            "GitHub adds a LABEL for any name but open/closed, which triggers nothing");
        Work(report).Start!.Reason.Should().Contain("not a native GitHub state");
    }

    /// <summary>
    /// A move nobody could make is the same answer whoever is asking. Sending a reader without
    /// runs.control to find a colleague who holds it would waste both their time.
    /// </summary>
    [Fact]
    public async Task WorkTicket_NotNativeAndWithoutRunsControl_SaysTheMoveIsImpossible()
    {
        var provider = new StartProvider("open");

        var report = await FileAsync(
            provider, Phase(), Routing(ByTag, "triage", tracker: TrackerType.GitHub),
            mayStartRuns: false);

        Work(report).Start!.Reason.Should().Contain("not a native GitHub state")
            .And.NotContain("ask someone who holds runs.control");
    }

    /// <summary>
    /// 2026-09-20-2ba8: a bug is CREATED with no framework label — deliberately, so the project's
    /// own rules decide which pipeline claims it — and the starter then puts the routing tag on,
    /// which is what makes the project claim it at all. Until this phase no tag-resolved project
    /// could ever claim a bug this dialog filed, and the reason said so every time.
    /// </summary>
    [Fact]
    public async Task BugOutcome_CarriesNoLabelsAtCreation_IsTaggedByTheStarterAndRoutes()
    {
        var provider = new StartProvider("To Do");

        var report = await FileAsync(
            provider, Bug(), Routing("operator-tag", "To Do"), mayStartRuns: true);

        provider.Labels.Should().Equal([("1", "operator-tag")]);
        var start = report.Filed.Should().ContainSingle().Subject.Start!;
        start.State.Should().Be(FiledStartState.Started);
        start.Reason.Should().StartWith("the tag 'operator-tag' was added to it; ");
    }

    [Fact]
    public async Task BugOutcome_Unresolvable_IsNeverMoved()
    {
        var provider = new StartProvider("New");

        await FileAsync(
            provider, Bug(), Routing(null, "To Do", ResolutionStrategy.AreaPath, @"Area\Widgets"),
            mayStartRuns: true);

        provider.Moves.Should().BeEmpty();
        provider.Labels.Should().BeEmpty("no label can satisfy an area-path resolution");
    }

    /// <summary>2026-09-22-b3d7: one ticket is filed and one ticket is moved.</summary>
    [Fact]
    public async Task ApprovedCut_MovesItsOneWorkTicketAndNothingElse()
    {
        var provider = new StartProvider("New");

        var report = await FileAsync(provider, Epic(), Routing(ByTag, "To Do"), mayStartRuns: true);

        report.Filed.Should().ContainSingle();
        provider.Moves.Should().Equal([("1", "To Do")], "only the work ticket is ever moved");
    }

    [Fact]
    public async Task Filing_ApprovedSetIsStoredBeforeTheTicketIsMoved()
    {
        var provider = new StartProvider("New");
        var store = new MoveWatchingStore(() => provider.Moves.Count);

        await FileAsync(provider, Epic(), Routing(ByTag, "To Do"), mayStartRuns: true, store: store);

        store.MovesWhenSaved.Should().Equal([0],
            "a ticket in a trigger status before its set exists is claimed and derives its own spec");
    }

    [Fact]
    public async Task Filing_StoreFails_TheTicketIsNotMovedAndTheOperatorIsTold()
    {
        var provider = new StartProvider("New");

        var report = await FileAsync(
            provider, Epic(), Routing(ByTag, "To Do"), mayStartRuns: true,
            store: new RefusingStore());

        report.Error.Should().Contain("Do not trigger");
        provider.Moves.Should().BeEmpty("the work ticket carries no specification yet");
    }

    /// <summary>
    /// The nullable start state is what an older filing deserializes into. It must read as unknown
    /// — the pane says nothing about it — rather than as a claim the server never made.
    /// </summary>
    [Fact]
    public void FilingRecord_WrittenBeforeTheStartState_ReadsAsUnknown()
    {
        const string legacy =
            """{"filed":[{"reference":"https://tracker.test/1","title":"p1"}],"error":null,"at":"2026-09-17T10:00:00Z","kind":"phase","notes":[]}""";

        var filing = System.Text.Json.JsonSerializer.Deserialize<SpecDialogFiling>(
            legacy, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        var ticket = filing!.Filed.Should().ContainSingle().Subject;
        ticket.Start.Should().BeNull();
        ticket.TicketId.Should().BeNull();
        ticket.Project.Should().BeNull();
    }

    /// <summary>The notice the thread and the transcript keep says it per ticket, in words.</summary>
    [Fact]
    public void FilingNotice_NamesWhatEachTicketBecame()
    {
        var report = new FilingReport(
            [new FiledTicket("https://tracker.test/1", "p1")
                { Start = new FiledWorkStart(FiledStartState.NotStarted, "nothing would route it.") }],
            Error: null);

        var notice = new SpecDialogOutcomeComposer()
            .ComposeFiled(Phase(), report).In(SpecDialogMarkup.For("slack"));

        notice.Should().Contain("not started").And.Contain("nothing would route it.");
    }

    /// <summary>
    /// A bare newline inside a markdown list item is a SOFT break, so the reason would run on
    /// after the title on the page; a nested bullet reads on the page and in Slack alike. The
    /// property is a rendering of two fields already on the record, so it is not a third field on
    /// the session row or on the push.
    /// </summary>
    [Fact]
    public void FiledWorkStart_TheNote_IsANestedBulletAndIsNotSerialized()
    {
        var start = new FiledWorkStart(FiledStartState.NotStarted, "nothing would route it.");

        start.Note.Should().Be("\n  - not started: nothing would route it.");
        System.Text.Json.JsonSerializer.Serialize(
                start,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))
            .Should().NotContain("note", "a rendering is not a field on the row or the push");
    }

    private static FiledTicket Work(FilingReport report) => report.Filed[0];

    private static async Task<FilingReport> FileAsync(
        StartProvider provider, OutcomeProposal proposal, AgentSmithConfig config,
        bool mayStartRuns, ISpecApprovalStore? store = null, IStartupFindings? findings = null,
        IEnvelopeProjectResolver? resolver = null)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider);
        var starter = FiledWorkDoubles.Starter(config, findings, resolver);
        var filer = new OutcomeTicketFiler(
            config, factory.Object, new PhaseTicketRenderer(), new BugTicketRenderer(),
            new EpicChildOrderer(), ApprovedSetDoubles.SetFiler(store, starter),
            starter, ApprovedSetDoubles.Kinds(), NullLogger<OutcomeTicketFiler>.Instance);
        return await filer.FileAsync(State(), proposal, mayStartRuns, CancellationToken.None);
    }

    /// <summary>
    /// ONE catalog, as the server has: the filer resolves the session's project out of it and the
    /// starter resolves the filed ticket against the very same triggers the poller would read.
    /// </summary>
    private static AgentSmithConfig Routing(
        string? tag, string? firstStatus = null,
        ResolutionStrategy strategy = ResolutionStrategy.Tag, string? areaPath = null,
        string? secondStatus = null, TrackerType tracker = TrackerType.AzureDevOps,
        bool noResolution = false, bool noTrigger = false,
        Dictionary<string, string>? pipelineFromLabel = null, string? otherProjectTag = null)
    {
        var trigger = new WebhookTriggerConfig
        {
            DefaultPipeline = pipelineFromLabel is null ? "code" : null,
            PipelineFromLabel = pipelineFromLabel,
            TriggerStatuses = firstStatus is null ? [] : secondStatus is null
                ? [firstStatus] : [firstStatus, secondStatus],
            ProjectResolution = noResolution ? null : new ProjectResolutionConfig
            {
                Strategy = strategy,
                Value = strategy == ResolutionStrategy.Tag ? tag ?? "" : areaPath ?? "",
            },
        };
        var project = new ResolvedProject
        {
            Name = "proj",
            DefaultPipeline = "code",
            Tracker = new TrackerConnection { Name = "sample-tracker", Type = tracker },
            Repos = [new RepoConnection { Name = "sample-api" }],
            GithubTrigger = Slot(tracker, TrackerType.GitHub, trigger, noTrigger),
            GitlabTrigger = Slot(tracker, TrackerType.GitLab, trigger, noTrigger),
            AzuredevopsTrigger = Slot(tracker, TrackerType.AzureDevOps, trigger, noTrigger),
            JiraTrigger = Slot(tracker, TrackerType.Jira, AsJira(trigger), noTrigger),
        };
        var projects = new Dictionary<string, ResolvedProject>(StringComparer.Ordinal)
        {
            ["proj"] = project,
        };
        if (otherProjectTag is not null)
            projects["neighbour"] = new ResolvedProject
            {
                Name = "neighbour",
                DefaultPipeline = "code",
                Tracker = new TrackerConnection { Name = "sample-tracker", Type = tracker },
                AzuredevopsTrigger = new WebhookTriggerConfig
                {
                    DefaultPipeline = "code",
                    TriggerStatuses = ["To Do"],
                    ProjectResolution = new ProjectResolutionConfig
                    {
                        Strategy = ResolutionStrategy.Tag, Value = otherProjectTag,
                    },
                },
            };
        return new AgentSmithConfig
        {
            Projects = projects,
            PipelineTriggers = PipelineTriggerMap.Empty,
        };
    }

    /// <summary>2026-09-20-2ba8: the trigger hangs off the slot its TRACKER reads, because the
    /// tagger selects the trigger by platform exactly as the starter and the poller do.</summary>
    private static T? Slot<T>(TrackerType tracker, TrackerType slot, T trigger, bool noTrigger)
        where T : WebhookTriggerConfig => tracker == slot && !noTrigger ? trigger : null;

    /// <summary>Jira's slot takes its own subtype; everything this fixture sets lives on the base.</summary>
    private static JiraTriggerConfig AsJira(WebhookTriggerConfig trigger) => new()
    {
        DefaultPipeline = trigger.DefaultPipeline,
        PipelineFromLabel = trigger.PipelineFromLabel,
        TriggerStatuses = trigger.TriggerStatuses,
        ProjectResolution = trigger.ProjectResolution,
    };

    private static PhaseOutcome Phase() => new(Draft("p9000a"));

    private static BugOutcome Bug() =>
        new(new BugTicketDraft("the widget throws", "it throws on save", null));

    private static EpicOutcome Epic() =>
        new(new PhaseDraft("p9000", "Widget platform",
                "phase: p9000\ngoal: \"Widget platform\"\ndone:\n  - \"reachable\"", [])
            { Done = ["reachable"] },
            [Draft("p9000a"), Draft("p9000b")]);

    private static PhaseDraft Draft(string id) =>
        new(id, $"slice {id}", $"phase: {id}\ngoal: \"slice {id}\"\ndone:\n  - \"{id} done\"", [])
        { Done = [$"{id} done"] };

    private static ConversationState State() => new()
    {
        JobId = "job-1",
        ChannelId = "C1",
        UserId = "sample.user",
        Platform = "dashboard",
        Project = "proj",
        TicketId = string.Empty,
        StartedAt = DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// A tracker that creates, is read back for its status, and remembers every move asked of it.
    /// The status it reports AFTER a move is the point: "started" is claimed from what the tracker
    /// says, never from the fact that a request was sent.
    /// <para>
    /// 2026-09-20-2ba8: it takes a LABEL on an existing ticket too, because all four shipped
    /// providers do. <see cref="CanLabel"/> is the one that cannot — the port's do-nothing
    /// default — and it must never be mistaken for a tag that landed.
    /// </para>
    /// </summary>
    private sealed class StartProvider(string status) : ITicketProvider
    {
        private readonly List<string> _statuses = [];
        private string _status = status;

        public List<(string Ticket, string Status)> Moves { get; } = [];

        public List<(string Ticket, string Label)> Labels { get; } = [];

        public bool CanLabel { get; init; } = true;

        public Exception? LabelError { get; init; }

        public bool MoveTakesEffect { get; init; } = true;

        public Exception? MoveError { get; init; }

        public string ProviderType => "recording";

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ConnectionProbeResult.Reachable(0));

        public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken) =>
            Task.FromResult(new Ticket(ticketId, "t", string.Empty, null, _status, ProviderType, []));

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels, string? kind, CancellationToken ct)
        {
            _statuses.Add(title);
            return Task.FromResult(new CreatedTicket(
                new TicketId(_statuses.Count.ToString()), $"https://tracker.test/{_statuses.Count}"));
        }

        public Task<bool> AddLabelAsync(TicketId ticketId, string label, CancellationToken ct)
        {
            if (LabelError is not null) throw LabelError;
            if (!CanLabel) return Task.FromResult(false);
            Labels.Add((ticketId.Value, label));
            return Task.FromResult(true);
        }

        public Task<ParentLinkResult> LinkToParentAsync(
            CreatedTicket child, TicketId parent, CancellationToken ct) =>
            Task.FromResult(ParentLinkResult.Linked);

        public Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<bool> TransitionToAsync(TicketId ticketId, string statusName, CancellationToken ct)
        {
            if (MoveError is not null) throw MoveError;
            Moves.Add((ticketId.Value, statusName));
            if (MoveTakesEffect) _status = statusName;
            return Task.FromResult(true);
        }

        public Task<TicketFinalizeResult> FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus, CancellationToken ct) =>
            Task.FromResult(TicketFinalizeResult.Moved());
    }

    /// <summary>Records how many moves — and, since 2026-09-20-2ba8, how many tags — had been
    /// written at the moment the set was stored. Both must be zero.</summary>
    private sealed class MoveWatchingStore(Func<int> movesSoFar, Func<int>? labelsSoFar = null)
        : ISpecApprovalStore
    {
        public List<int> MovesWhenSaved { get; } = [];

        public List<int> LabelsWhenSaved { get; } = [];

        public Task<SpecApprovalRecord?> GetAsync(string tracker, string key, CancellationToken ct) =>
            Task.FromResult<SpecApprovalRecord?>(null);

        public Task SaveAsync(SpecApprovalRecord record, CancellationToken ct)
        {
            MovesWhenSaved.Add(movesSoFar());
            LabelsWhenSaved.Add(labelsSoFar?.Invoke() ?? 0);
            return Task.CompletedTask;
        }
    }

    /// <summary>Records how many labels were on the ticket at the moment the resolution ran, over
    /// the REAL resolver — what it answers has to be what the poller would answer.</summary>
    private sealed class LabelWatchingResolver(IEnvelopeProjectResolver inner, Func<int> labelsSoFar)
        : IEnvelopeProjectResolver
    {
        public List<int> LabelsWhenResolved { get; } = [];

        public IReadOnlyList<ProjectMatch> Resolve(
            AgentSmithConfig config, IncomingTicketEnvelope envelope)
        {
            LabelsWhenResolved.Add(labelsSoFar());
            return inner.Resolve(config, envelope);
        }
    }

    private sealed class RefusingStore : ISpecApprovalStore
    {
        public Task<SpecApprovalRecord?> GetAsync(string tracker, string key, CancellationToken ct) =>
            Task.FromResult<SpecApprovalRecord?>(null);

        public Task SaveAsync(SpecApprovalRecord record, CancellationToken ct) =>
            throw new InvalidOperationException("the approvals table is gone");
    }
}
