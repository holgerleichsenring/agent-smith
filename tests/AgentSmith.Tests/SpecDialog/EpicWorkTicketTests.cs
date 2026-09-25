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
/// 2026-09-17-0e79d: an approved cut is ONE piece of work. It files one phase-labelled WORK
/// ticket carrying the whole approved set under its own spec key.
/// <para>
/// 2026-09-22-b3d7: and ONE ticket is all it files. The slice records it used to file beside the
/// work ticket were a second copy of that ticket's own "## Slices" section, so they are gone with
/// their filer, their label writer, their parent links and the comment that listed them.
/// </para>
/// </summary>
public sealed class EpicWorkTicketTests
{
    [Fact]
    public async Task Filing_AnApprovedCutOfThreeSlices_CreatesExactlyOneTicket()
    {
        var provider = new RecordingProvider();

        var report = await FileAsync(
            provider, Epic(Slice("p9000a"), Slice("p9000b"), Slice("p9000c")));

        provider.Created.Should().ContainSingle("a cut is one piece of work, whatever the slice count");
        provider.Created[0].Labels.Should().Equal(
            [FiledTicketLabels.ApprovedSetStamp],
            "the work ticket is what a run picks up, and the stamp is what routes it there");
        provider.Comments.Should().BeEmpty("there are no records to list on it");
        report.Filed.Should().ContainSingle();
        report.Notes.Should().BeEmpty();
    }

    /// <summary>
    /// 2026-09-22-766b: the word is gone from the FILING, and it is still read — a person types
    /// it. That is why this pin matters more rather than less: the word binds phase execution, so
    /// a filing that wrote it would put a framework word on somebody else's board AND hand every
    /// filed ticket a second binding key. The stamp beside it already says the same thing — that
    /// somebody approved a specification for this ticket, which is exactly what "this ticket is
    /// phase execution" means.
    /// </summary>
    [Fact]
    public async Task Filing_AFiledTicket_CarriesNoPhaseWord()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")));
        await FileRawAsync(provider, new PhaseOutcome(Slice("p9000c")));

        provider.Created.Should().HaveCount(2).And.OnlyContain(
            c => !c.Labels.Contains("phase", StringComparer.OrdinalIgnoreCase),
            "a cut's work ticket and a lone phase are filed with the same label set");
    }

    /// <summary>
    /// And the stamp is still there and is the ONLY thing there — it is the one key a filing
    /// writes. Dropping it would take both of its jobs with it: the routing bind and the guard
    /// against a lost hand-off being re-derived from a description anyone can edit. Anything
    /// beside it would be a word an operator's board gained without choosing it.
    /// </summary>
    [Fact]
    public async Task Filing_AFiledTicket_CarriesOnlyTheApprovalStamp()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")));
        await FileRawAsync(provider, new PhaseOutcome(Slice("p9000c")));

        provider.Created.Should().HaveCount(2).And.OnlyContain(
            c => c.Labels.Count == 1 && c.Labels[0] == FiledTicketLabels.ApprovedSetStamp);
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

    /// <summary>
    /// 2026-09-22-b3d7: this section is what the slice records duplicated, so it is now the ONLY
    /// place a person reads a slice on its own — every id, every goal and every requires: edge.
    /// </summary>
    [Fact]
    public async Task Filing_AnApprovedCut_ListsEverySliceAndItsRequiresEdgesInTheTicketBody()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a", requires: ["p9000b"]), Slice("p9000b")));

        var body = provider.Created[0].Body;
        body.Should().Contain("## Slices");
        body.Should().Contain("`p9000a` slice p9000a (requires: p9000b)")
            .And.Contain("`p9000b` slice p9000b");
        body.IndexOf("p9000b", StringComparison.Ordinal).Should()
            .BeLessThan(body.IndexOf("p9000a", StringComparison.Ordinal),
                "the listing is the order the one run works them in");
    }

    [Fact]
    public async Task Filing_AnApprovedCut_StoresTheWholeOrderedSetUnderThatTicket()
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

    /// <summary>
    /// 2026-09-22-b3d7: the label's WRITER is gone. Its reader stays for the two generations that
    /// already carry it, and this is what says the framework adds no third one.
    /// </summary>
    [Fact]
    public async Task Filing_AnApprovedCut_WritesNoTicketCarryingTheRecordLabel()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b"), Slice("p9000c")));

        provider.Created.SelectMany(c => c.Labels).Should().NotContain(PhaseTicketRenderer.EpicLabel);
    }

    /// <summary>
    /// 2026-09-17-0e79a: the loud miss and the source precedence both key on the APPROVED-SET
    /// stamp, not on the phase label — a hand-written phase ticket carries the label too and its
    /// spec legitimately lives in its description. Without the stamp an epic's work ticket would
    /// be the one filed shape where the gate never fires and a fenced block pasted into the
    /// description by anyone with tracker access becomes the spec again.
    /// </summary>
    [Fact]
    public async Task EpicApproval_WorkTicketCarriesTheApprovedSetStamp()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")));

        FiledTicketLabels.CarriesApprovedSet(provider.Created[0].Labels).Should().BeTrue(
            "the run that picks this ticket up is held to the set that was approved for it");
    }

    /// <summary>
    /// The gate reads a fetched TICKET, so the stamp the filer writes has to be the one it looks
    /// for: a work ticket with no set reaching DeriveSpec must park rather than derive a guess.
    /// </summary>
    [Fact]
    public async Task EpicApproval_WorkTicketWithNoSet_IsTheLoudMiss()
    {
        var provider = new RecordingProvider();
        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")));
        var gate = new FiledTicketSpecGate(NullLogger<FiledTicketSpecGate>.Instance);

        var handback = gate.MissingSet(
            Fetched(provider.Created[0].Labels), new SpecSetKey("recording-1"), null,
            SpecSetBranchState.NothingAtThePath);

        handback!.Case.Should().Be(SpecHandbackCase.SpecificationMissingFromBranch);
        handback.Reason.Should().Contain(FiledTicketLabels.ApprovedSetStamp);
    }

    private static Ticket Fetched(IReadOnlyList<string> labels) =>
        new(new TicketId("1"), "t", string.Empty, null, "open", "recording", [.. labels]);

    /// <summary>
    /// A parent stamp would make the run resolve the parent's rung as its base and publish it.
    /// One run needs one branch cut from its own base, which is what the ladder does when it
    /// falls through.
    /// </summary>
    [Fact]
    public async Task EpicApproval_WorkTicket_CarriesNoParentStamp()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b", requires: ["p9000a"])));

        FiledTicketLabels.ParentId(provider.Created[0].Labels).Should().BeNull();
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
            .And.Contain("job-1", "the pointer records the conversation the set was approved in");
    }

    /// <summary>
    /// A ticket with no stored set is the broken hand-off: it carries the phase label and no
    /// specification, so its own run stops at the spec gate. The operator is told which ticket
    /// that is instead of being handed a complete-looking filing.
    /// <para>
    /// 2026-09-22-b3d7: FOR A PHASE AS WELL AS FOR A CUT. It is one hazard and one filing path,
    /// and only the cut's path used to name the ticket.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Filing_AStoreThatFails_NamesTheTicketThatMustNotBeTriggered_ForAPhaseAsWellAsACut(
        bool cut)
    {
        var provider = new RecordingProvider();
        OutcomeProposal proposal = cut
            ? Epic(Slice("p9000a"), Slice("p9000b"))
            : new PhaseOutcome(Slice("p9000a"));

        var report = await FileRawAsync(
            provider, proposal,
            new ThrowingStore(new InvalidOperationException("the approvals table is gone")));

        report.Error.Should().Contain("https://tracker.test/1")
            .And.Contain("Do not trigger").And.Contain("the approvals table is gone");
        provider.Created.Should().ContainSingle();
    }

    /// <summary>
    /// 2026-09-22-b3d7: the refusal is what keeps the records ALREADY on a board unroutable —
    /// both generations of them, the epic parent summaries and the slice records, and neither
    /// carries any other framework label for a rule to key on.
    /// </summary>
    [Fact]
    public void ProjectResolver_AnEnvelopeCarryingTheRecordLabel_StillResolvesToNoProject() =>
        Resolve([PhaseTicketRenderer.EpicLabel]).Should().BeEmpty(
            "a record in a trigger status would otherwise be claimed like any ticket");

    [Fact]
    public async Task WorkTicket_FromTheFilersOwnOutput_RoutesToTheCodePreset()
    {
        var provider = new RecordingProvider();
        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")));

        Resolve(provider.Created[0].Labels).Should().ContainSingle()
            .Which.PipelineName.Should().Be(PipelinePresets.CodeName);
    }

    /// <summary>
    /// The value is what the refusal reads, and every record already on a tracker carries it —
    /// the epic parent summaries filed before 2026-09-17-0e79d and the slice records filed until
    /// 2026-09-22-b3d7. Renaming the constant would make each of them routable overnight.
    /// </summary>
    [Fact]
    public void RecordLabel_KeepsItsValue_SoTheRecordsAlreadyFiledStayUnroutable()
    {
        PhaseTicketRenderer.EpicLabel.Should().Be("phase-epic");
        Resolve(["phase-epic"]).Should().BeEmpty();
    }

    /// <summary>
    /// 2026-09-22-b3d7: the notice counts what the filing actually created — one ticket — and the
    /// slices as what that one ticket carries. Counting linked records was true of the shape the
    /// filer stopped producing.
    /// </summary>
    [Fact]
    public void FilingNotice_AnApprovedCut_CountsOneTicketAndNoRecords()
    {
        var report = new FilingReport([new FiledTicket("https://tracker.test/1", "p9000")], Error: null);

        var notice = new SpecDialogOutcomeComposer()
            .ComposeFiled(Epic(Slice("p9000a"), Slice("p9000b")), report)
            .In(SpecDialogMarkup.For("slack"));

        notice.Should().Contain("one work ticket (`p9000`)").And.Contain("carrying 2 slice(s)");
        notice.Should().NotContain("record", "nothing beside that ticket was filed");
        notice.Should().NotContain("child phases", "no slice of a cut is picked up by a run of its own");
    }

    /// <summary>
    /// 2026-09-18-b4f0: the ROLE is a property of the CALL SITE, which is why each site names its
    /// own. 2026-09-22-b3d7: the `record` role went with the records, so a cut's work ticket is
    /// the only thing a cut's filing creates and `work` is the only kind it can send.
    /// </summary>
    [Fact]
    public async Task Create_ACutsFiling_UsesTheWorkKindForItsOneTicket()
    {
        var provider = new RecordingProvider();

        await FileAsync(provider, Epic(Slice("p9000a"), Slice("p9000b")), kinds: Kinds(
            ("work", "Feature"), ("phase", "User Story")));

        provider.Created.Should().ContainSingle()
            .Which.Kind.Should().Be("Feature", "the work ticket is the level a run picks up");
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
    /// 2026-09-17-0e79d: the single-phase path must not have moved with the cut's. One approved
    /// phase is still one phase-labelled ticket and one stored set — which, since 2026-09-22-b3d7,
    /// is exactly what a cut files too.
    /// </summary>
    [Fact]
    public async Task PhaseApproval_SinglePhase_StillFilesOneTicket()
    {
        var provider = new RecordingProvider();
        var store = ApprovedSetDoubles.Store();

        var report = await FileRawAsync(provider, new PhaseOutcome(Slice("p9000a")), store);

        report.Error.Should().BeNull();
        provider.Created.Should().ContainSingle()
            .Which.Labels.Should().Equal(
                [FiledTicketLabels.ApprovedSetStamp],
                "a single approved phase is stamped exactly as an epic's work ticket is");
        provider.Comments.Should().BeEmpty("a filing posts no comment of its own");
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
            new EpicChildOrderer(), ApprovedSetDoubles.SetFiler(store),
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

        /// <summary>2026-09-18-b4f0: the KIND is recorded too — it is what crosses the port.</summary>
        public IReadOnlyList<(string Title, string Body, IReadOnlyList<string> Labels, string? Kind)> Created => _created;

        public List<(string Ticket, string Comment)> Comments { get; } = [];

        public string ProviderType => "recording";

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ConnectionProbeResult.Reachable(0));

        // 2026-09-22-b6ad: filing reads its own new ticket back, so the set it writes to the
        // branch is fingerprinted from what the TRACKER stored rather than from the body we sent.
        public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken) =>
            Task.FromResult(new Ticket(
                ticketId, _created[int.Parse(ticketId.Value) - 1].Title,
                _created[int.Parse(ticketId.Value) - 1].Body, null, "open", ProviderType, []));

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels, string? kind,
            CancellationToken cancellationToken)
        {
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

    private sealed class ThrowingStore(Exception error) : ISpecApprovalStore
    {
        public Task<SpecApprovalRecord?> GetAsync(
            string tracker, string key, CancellationToken cancellationToken) =>
            Task.FromResult<SpecApprovalRecord?>(null);

        public Task SaveAsync(SpecApprovalRecord record, CancellationToken cancellationToken) =>
            throw error;
    }
}
