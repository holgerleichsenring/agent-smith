using AgentSmith.Application.Services.Metrics;
using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
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
/// 2026-09-17-0e79d: an approved epic is ONE piece of work. It files one phase-labelled WORK
/// ticket carrying the whole approved set under its own spec key, and one RECORD per slice that
/// nothing routes and no machine reads — the tracker's link is what ties them together.
/// </summary>
public sealed class EpicWorkTicketTests
{
    [Fact]
    public async Task EpicApproval_FilesOneWorkTicketWithThePhaseLabel()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")));

        provider.Created.Should().HaveCount(3, "one work ticket, then one record per slice");
        provider.Created[0].Labels.Should().Equal(
            [PhaseTicketRenderer.PhaseLabel, FiledTicketLabels.ApprovedSetStamp],
            "the work ticket is what a run picks up; the records are not work");
    }

    [Fact]
    public async Task EpicApproval_WorkTicketTitleAndBody_ComeFromTheParentDraft()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")));

        provider.Created[0].Title.Should().Be("p9000: Widget platform");
        provider.Created[0].Body.Should().Contain("Widget platform")
            .And.Contain("the whole platform is reachable", "the parent's own done list travels")
            .And.NotContain("```", "a requirement body opens no fence");
    }

    [Fact]
    public async Task EpicApproval_WorkTicketBody_ListsTheSlicesInOrder()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a", requires: ["p9000b"]), Slice("p9000b")));

        var body = provider.Created[0].Body;
        body.Should().Contain("## Slices");
        body.IndexOf("p9000b", StringComparison.Ordinal).Should()
            .BeLessThan(body.IndexOf("p9000a", StringComparison.Ordinal),
                "the listing is the order the one run works them in");
    }

    [Fact]
    public async Task EpicApproval_StoresTheWholeSetUnderTheWorkTicketsKey()
    {
        var provider = new RecordingProvider();
        var store = ApprovedSetDoubles.Store();

        await FileAsync(provider, Epic(Slice("p9000a", requires: ["p9000b"]), Slice("p9000b")), store);

        var record = await store.GetAsync("sample-tracker", SpecSetKey.For("azuredevops", "1").Value, default);
        record.Should().NotBeNull("the run computes this key from the WORK ticket it was spawned on");
        record!.Set.Source.Should().Be(SpecSource.Approved);
        record.Set.Phases.Select(p => p.PhaseId).Should().Equal(["p9000b", "p9000a"],
            "the set is stored in the order the sequence will splice it");
        record.Approval!.Conversation.Should().Be("job-1");
    }

    [Fact]
    public async Task EpicApproval_SliceRecords_CarryTheRecordLabelAndNoPhaseLabel()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")));

        provider.Created.Skip(1).Should().OnlyContain(
            c => c.Labels.Count == 1 && c.Labels[0] == PhaseTicketRenderer.EpicLabel,
            "the phase label hard-binds routing, and a bare ticket is routed by the project's own rules");
    }

    /// <summary>
    /// 2026-09-17-0e79a: the loud miss and the source precedence both key on the APPROVED-SET
    /// stamp, not on the phase label — a hand-written phase ticket carries the label too and its
    /// spec legitimately lives in its description. Without the stamp an epic's work ticket would
    /// be the one filed shape where the gate never fires and a fenced block pasted into the
    /// description by anyone with tracker access becomes the spec again.
    /// <para>
    /// A slice record must NOT carry it: it has no set of its own, so the gate would hold it to a
    /// hand-off that was never made.
    /// </para>
    /// </summary>
    [Fact]
    public async Task EpicApproval_WorkTicketCarriesTheApprovedSetStamp_AndNoRecordDoes()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")));

        FiledTicketLabels.CarriesApprovedSet(provider.Created[0].Labels).Should().BeTrue(
            "the run that picks this ticket up is held to the set that was approved for it");
        provider.Created.Skip(1).Should().OnlyContain(
            c => !FiledTicketLabels.CarriesApprovedSet(c.Labels),
            "a record carries no set of its own, so nothing may hold it to one");
    }

    /// <summary>
    /// The gate reads a fetched TICKET, so the stamp the filer writes has to be the one it looks
    /// for: a work ticket with no set reaching DeriveSpec must fail loudly, and a record must be
    /// invisible to the rule entirely.
    /// </summary>
    [Fact]
    public async Task EpicApproval_WorkTicketWithNoSet_IsTheLoudMiss_AndARecordIsNot()
    {
        var provider = new RecordingProvider();
        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")));
        var gate = new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance);

        gate.MissingSet(Fetched(provider.Created[0].Labels)).Should()
            .NotBeNull().And.Subject.ToString().Should().Contain(FiledTicketLabels.ApprovedSetStamp);
        gate.MissingSet(Fetched(provider.Created[1].Labels)).Should().BeNull();
    }

    private static Ticket Fetched(IReadOnlyList<string> labels) =>
        new(new TicketId("1"), "t", string.Empty, null, "open", "recording", [.. labels]);

    [Fact]
    public async Task EpicApproval_SliceRecords_CarryNoParentOrPredecessorStamp()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b", requires: ["p9000a"])));

        provider.Created.Skip(1).SelectMany(c => c.Labels).Should().NotContain(
            l => l.StartsWith(FiledTicketLabels.ParentPrefix, StringComparison.Ordinal)
                || l.StartsWith(FiledTicketLabels.PredecessorPrefix, StringComparison.Ordinal),
            "no machine reads a record: it never routes, never runs and is not a rung");
    }

    /// <summary>
    /// A parent stamp would make the run resolve the parent's rung as its base and publish it.
    /// One run needs one branch cut from its own base, which is what the ladder does when it
    /// falls through; and with no predecessor stamps the gate has nothing to hold.
    /// </summary>
    [Fact]
    public async Task EpicApproval_WorkTicket_CarriesNoParentStamp()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b", requires: ["p9000a"])));

        FiledTicketLabels.ParentId(provider.Created[0].Labels).Should().BeNull();
        FiledTicketLabels.PredecessorIds(provider.Created[0].Labels).Should().BeEmpty();
    }

    [Fact]
    public async Task EpicApproval_SliceRecordBody_CarriesItsDoneListAndNoFence()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")));

        var body = provider.Created[1].Body;
        AcceptanceCriteriaSection.Read(body).Should().Equal("slice p9000a is finished");
        body.Should().NotContain("```", "a record is read by a person, not extracted by a deriver");
        body.Should().NotContain(PhaseTicketRenderer.SpecificationHeading,
            "no run works a record, so nothing is ever published to a branch of its own");
    }

    /// <summary>
    /// The one ticket a run works from must say where its specification lives — the body is not
    /// the spec, the approved record is, and a reader of an epic's work ticket must not have to
    /// know that an epic keeps it somewhere a filed phase does not.
    /// </summary>
    [Fact]
    public async Task EpicApproval_WorkTicketBody_PointsAtTheApprovedSpecification()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")));

        provider.Created[0].Body.Should().Contain(PhaseTicketRenderer.SpecificationHeading)
            .And.Contain(SpecSetKey.Root, "the run publishes the set to the ticket branch under it")
            .And.Contain("job-1", "a change to the set is made in the conversation that approved it");
    }

    /// <summary>
    /// The set is what the run works from and the work ticket is what a poller picks up, so the
    /// order matters: a ticket that exists before its set is a phase-labelled ticket the spec
    /// gate fails loudly on, for exactly as long as the window is open.
    /// </summary>
    [Fact]
    public async Task EpicApproval_TheSetIsStored_BeforeAnyRecordIsFiled()
    {
        var provider = new RecordingProvider();
        var store = new ObservingStore(() => provider.Created.Count);

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")), store);

        store.CreatedWhenSaved.Should().Equal([1],
            "the work ticket exists and no record does yet when the set is written");
    }

    /// <summary>
    /// A work ticket with no stored set is the broken hand-off: it carries the phase label and no
    /// specification, so its own run stops at the spec gate. The operator is told which ticket
    /// that is instead of being handed a complete-looking epic.
    /// </summary>
    [Fact]
    public async Task EpicApproval_TheSetCannotBeStored_IsAnErrorNamingTheWorkTicket()
    {
        var provider = new RecordingProvider();

        var report = await FileRawAsync(
            provider, Epic(Slice("p9000a"), Slice("p9000b")),
            new ThrowingStore(new InvalidOperationException("the approvals table is gone")));

        report.Error.Should().Contain("https://tracker.test/1")
            .And.Contain("Do not trigger").And.Contain("the approvals table is gone");
        provider.Created.Should().ContainSingle(
            "no record is filed behind a set that was never stored");
    }

    /// <summary>
    /// 2026-09-17-042ea made a failed LINK a note for this reason and the create path was left
    /// out. By the time a record is filed the work ticket exists and carries the whole approved
    /// set, so the epic is complete and runnable: an error would offer a retry, and the retry
    /// files a SECOND work ticket with a second stored set — two runs, two pull requests per
    /// repository.
    /// </summary>
    [Fact]
    public async Task EpicApproval_ARecordThatCannotBeFiled_IsANoteNotTheFilingsError()
    {
        var provider = new RecordingProvider
        {
            ThrowOnCreateNumber = 2,
            CreateError = new TaskCanceledException("the record create timed out"),
        };

        var report = await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")));

        report.Filed.Should().HaveCount(2, "the work ticket and the record that did land");
        provider.Created.Should().HaveCount(2, "the slice after the failed one is still recorded");
        report.Notes.Should().ContainSingle().Which.Should()
            .Contain("p9000a").And.Contain("the record create timed out")
            .And.Contain("the run works this slice either way");
    }

    [Fact]
    public async Task EpicApproval_WorkTicket_GetsOneCommentNamingEachRecord()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")));

        provider.Comments.Should().ContainSingle().Which.Should().Match<(string Ticket, string Comment)>(
            c => c.Ticket == "1"
                && c.Comment.Contains("https://tracker.test/2", StringComparison.Ordinal)
                && c.Comment.Contains("https://tracker.test/3", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SliceRecord_FromTheFilersOwnOutput_IsRefusedByTheProjectResolver()
    {
        var provider = new RecordingProvider();
        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")));

        Resolve(provider.Created[1].Labels).Should().BeEmpty(
            "a record in a trigger status would otherwise be claimed like any ticket");
    }

    [Fact]
    public async Task WorkTicket_FromTheFilersOwnOutput_RoutesToTheCodePreset()
    {
        var provider = new RecordingProvider();
        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")));

        Resolve(provider.Created[0].Labels).Should().ContainSingle()
            .Which.PipelineName.Should().Be(PipelinePresets.PhaseExecutionName);
    }

    /// <summary>
    /// The value is what the refusal reads, and every epic parent already on a tracker carries it.
    /// Renaming the constant would make each of them routable overnight.
    /// </summary>
    [Fact]
    public void RecordLabel_KeepsItsValue_SoAlreadyFiledParentsStayUnroutable()
    {
        PhaseTicketRenderer.EpicLabel.Should().Be("phase-epic");
        Resolve(["phase-epic"]).Should().BeEmpty();
    }

    /// <summary>
    /// 2026-09-17-0e79d: the filed notice counted the slices as runnable children — the shape the
    /// filer stopped producing. One work ticket runs; the records beside it are read.
    /// </summary>
    [Fact]
    public void EpicFiledNotice_NamesOneWorkTicketAndItsSliceRecords()
    {
        var report = new FilingReport([new FiledTicket("https://tracker.test/1", "p9000")], Error: null);

        var notice = new SpecDialogOutcomeComposer()
            .ComposeFiled(Epic(Slice("p9000a"), Slice("p9000b")), report)
            .In(SpecDialogMarkup.For("slack"));

        notice.Should().Contain("one work ticket (`p9000`)").And.Contain("2 linked slice record(s)");
        notice.Should().NotContain("child phases", "no child of an epic is picked up by a run");
    }

    /// <summary>
    /// 2026-09-18-b4f0: ONE create serves three hierarchy levels. The work ticket and the slice
    /// records that hang UNDER it are filed by the same method, so a single tracker-wide kind
    /// would make the parent and its children one kind and the link between them a same-level
    /// link. The role is a property of the CALL SITE, which is why each site can name its own.
    /// </summary>
    [Fact]
    public async Task Create_AnEpicFiling_UsesTheWorkKindForTheTicketAndTheRecordKindForItsSlices()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")), kinds: Kinds(
            ("work", "Feature"), ("record", "Task")));

        provider.Created[0].Kind.Should().Be("Feature", "the work ticket is the level a run picks up");
        provider.Created.Skip(1).Should().OnlyContain(c => c.Kind == "Task",
            "each record hangs under that ticket and must not be raised to its level");
    }

    /// <summary>
    /// The half of the incident that is not about states: raising the work ticket must not drag
    /// the records up with it. An unmapped role creates what it created before — it does not
    /// inherit the kind chosen for the ticket it is linked to.
    /// </summary>
    [Fact]
    public async Task Create_AConfiguredWorkKind_DoesNotRaiseTheSliceRecords()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")), kinds: Kinds(("work", "Epic")));

        provider.Created[0].Kind.Should().Be("Epic");
        provider.Created.Skip(1).Should().OnlyContain(c => c.Kind == null,
            "an unmapped role sends no kind, and the provider creates what it created before");
    }

    /// <summary>
    /// A bug is its own role: it is the one filing both trackers ship a dedicated native type
    /// for, and this codebase already files it apart. A lone phase is a fourth role again — it
    /// carries the same labels as an epic's work ticket, so nothing but the call site can tell
    /// the two apart.
    /// </summary>
    [Fact]
    public async Task Create_ABugFiling_SendsTheBugKind_AndALonePhaseFilingSendsTheSingleTicketKind()
    {
        var kinds = Kinds(("bug", "Bug"), ("phase", "User Story"), ("work", "Feature"));
        var bugs = new RecordingProvider();
        var phases = new RecordingProvider();

        var bugReport = await FileRawAsync(
            bugs, new BugOutcome(new BugTicketDraft("The widget is lost", "It vanished.", null)),
            ApprovedSetDoubles.Store(), kinds);
        var phaseReport = await FileRawAsync(
            phases, new PhaseOutcome(Slice("p9000a")), ApprovedSetDoubles.Store(), kinds);

        bugReport.Error.Should().BeNull();
        phaseReport.Error.Should().BeNull();
        bugs.Created.Should().ContainSingle().Which.Kind.Should().Be("Bug");
        phases.Created.Should().ContainSingle().Which.Kind.Should().Be("User Story",
            "a lone phase is not an epic's work ticket, even though their labels are identical");
    }

    private static Dictionary<string, string> Kinds(params (string Role, string Kind)[] entries) =>
        entries.ToDictionary(e => e.Role, e => e.Kind, StringComparer.Ordinal);

    private static IReadOnlyList<ProjectMatch> Resolve(IReadOnlyList<string> labels) =>
        new ProjectResolver(new AgentSmithMetrics(), new PipelineResolver())
            .Resolve(RoutingConfig(), Envelope(labels));

    private static IncomingTicketEnvelope Envelope(IReadOnlyList<string> labels) =>
        new() { TicketId = "1", Platform = "github", Labels = [.. labels, "bug"] };

    private static AgentSmithConfig RoutingConfig() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>(StringComparer.Ordinal)
        {
            ["app"] = new()
            {
                Name = "app",
                DefaultPipeline = "code",
                GithubTrigger = new WebhookTriggerConfig
                {
                    DefaultPipeline = "code",
                    TriggerStatuses = ["open"],
                    NeedsClarificationStatus = "question",
                    ProjectResolution = new ProjectResolutionConfig
                    {
                        Strategy = ResolutionStrategy.Tag,
                        Value = "bug",
                    },
                },
            },
        },
        PipelineTriggers = PipelineTriggerMap.Empty,
    };

    /// <summary>
    /// 2026-09-17-0e79d: the single-phase path must not have moved with the epic's. One approved
    /// phase is still one phase-labelled ticket and one stored set — no record beside it, because
    /// there is no cut to record.
    /// </summary>
    [Fact]
    public async Task PhaseApproval_SinglePhase_StillFilesOneWorkTicket()
    {
        var provider = new RecordingProvider();
        var store = ApprovedSetDoubles.Store();

        var report = await FileRawAsync(provider, new PhaseOutcome(Slice("p9000a")), store);

        report.Error.Should().BeNull();
        provider.Created.Should().ContainSingle()
            .Which.Labels.Should().Equal(
                [PhaseTicketRenderer.PhaseLabel, FiledTicketLabels.ApprovedSetStamp],
                "a single approved phase is stamped exactly as an epic's work ticket is");
        provider.Comments.Should().BeEmpty("there are no records to name");
        var record = await store.GetAsync("sample-tracker", SpecSetKey.For("azuredevops", "1").Value, default);
        record!.Set.Phases.Should().ContainSingle().Which.PhaseId.Should().Be("p9000a");
    }

    private static async Task<FilingReport> FileAsync(
        RecordingProvider provider, EpicOutcome epic, ISpecApprovalStore? store = null,
        IReadOnlyDictionary<string, string>? kinds = null)
    {
        var report = await FileRawAsync(provider, epic, store, kinds);
        report.Error.Should().BeNull();
        return report;
    }

    private static async Task<FilingReport> FileRawAsync(
        RecordingProvider provider, OutcomeProposal proposal, ISpecApprovalStore? store = null,
        IReadOnlyDictionary<string, string>? kinds = null)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider);
        var filer = new OutcomeTicketFiler(
            Config(kinds), factory.Object, new PhaseTicketRenderer(), new BugTicketRenderer(),
            ApprovedSetDoubles.EpicFiler(store), ApprovedSetDoubles.Recorder(store),
            FiledWorkDoubles.Starter(), ApprovedSetDoubles.Kinds(), NullLogger<OutcomeTicketFiler>.Instance);
        return await filer.FileAsync(State(), proposal, false, CancellationToken.None);
    }

    private static AgentSmithConfig Config(IReadOnlyDictionary<string, string>? kinds = null) => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["proj"] = new()
            {
                Name = "proj",
                Tracker = new TrackerConnection
                {
                    Name = "sample-tracker",
                    Type = TrackerType.AzureDevOps,
                    WorkItemKinds = kinds ?? new Dictionary<string, string>(),
                },
                Repos = [new RepoConnection { Name = "sample-api" }],
            },
        },
    };

    private static EpicOutcome Epic(params PhaseDraft[] slices) =>
        new(new PhaseDraft("p9000", "Widget platform",
            "phase: p9000\ngoal: \"Widget platform\"\ndone:\n  - \"the whole platform is reachable\"",
            []) { Done = ["the whole platform is reachable"] }, slices);

    private static PhaseDraft Slice(string id, IReadOnlyList<string>? requires = null) =>
        new(id, $"slice {id}",
            $"phase: {id}\ngoal: \"slice {id}\"\ndone:\n  - \"slice {id} is finished\"",
            requires ?? []) { Done = [$"slice {id} is finished"] };

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

    private sealed class RecordingProvider : ITicketProvider
    {
        private readonly List<(string Title, string Body, IReadOnlyList<string> Labels, string? Kind)> _created = [];
        private int _attempts;

        /// <summary>2026-09-18-b4f0: the KIND is recorded too — it is what crosses the port.</summary>
        public IReadOnlyList<(string Title, string Body, IReadOnlyList<string> Labels, string? Kind)> Created => _created;

        public List<(string Ticket, string Comment)> Comments { get; } = [];

        /// <summary>Which create call throws — 1 is the work ticket, 2 the first record.</summary>
        public int? ThrowOnCreateNumber { get; init; }

        public Exception? CreateError { get; init; }

        public string ProviderType => "recording";

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ConnectionProbeResult.Reachable(0));

        public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels, string? kind,
            CancellationToken cancellationToken)
        {
            if (++_attempts == ThrowOnCreateNumber)
                throw CreateError ?? new InvalidOperationException("the tracker refused it");
            _created.Add((title, description, labels, kind));
            return Task.FromResult(new CreatedTicket(
                new TicketId(_created.Count.ToString()), $"https://tracker.test/{_created.Count}"));
        }

        public Task<ParentLinkResult> LinkToParentAsync(
            CreatedTicket child, TicketId parent, CancellationToken cancellationToken) =>
            Task.FromResult(ParentLinkResult.Linked);

        public Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken)
        {
            Comments.Add((ticketId.Value, comment));
            return Task.CompletedTask;
        }

        public Task<TicketFinalizeResult> FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken) =>
            Task.FromResult(TicketFinalizeResult.Moved());
    }

    /// <summary>Records how much had been filed at the moment the set was written.</summary>
    private sealed class ObservingStore(Func<int> createdSoFar) : ISpecApprovalStore
    {
        public List<int> CreatedWhenSaved { get; } = [];

        public Task<SpecApprovalRecord?> GetAsync(
            string tracker, string key, CancellationToken cancellationToken) =>
            Task.FromResult<SpecApprovalRecord?>(null);

        public Task SaveAsync(SpecApprovalRecord record, CancellationToken cancellationToken)
        {
            CreatedWhenSaved.Add(createdSoFar());
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingStore(Exception error) : ISpecApprovalStore
    {
        public Task<SpecApprovalRecord?> GetAsync(
            string tracker, string key, CancellationToken cancellationToken) =>
            Task.FromResult<SpecApprovalRecord?>(null);

        public Task SaveAsync(SpecApprovalRecord record, CancellationToken cancellationToken) =>
            throw error;
    }
}
