using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-22-b6ad: filing an approved specification writes it onto the ticket branch in the same
/// act that files the ticket, so the specification exists before any run does. Until this phase
/// the approved set was a database row between the approval and the first publish — nothing to
/// open, nothing to review and nothing to edit.
/// </summary>
public sealed class FiledSpecBranchTests
{
    private const string Key = "azuredevops-1";
    private const string Planned = SeriesPaths.Planned;
    private const string Branch = "agent-smith/1";
    private const string StoredTitle = "p9000a: the tracker's own wording";
    private const string StoredBody = "<p>what the tracker stored, not what we sent</p>";

    [Fact]
    public async Task Filing_APhaseApprovedInTheDialog_WritesTheSetToTheTicketBranch()
    {
        var sources = new RecordingBranchSources();

        var report = await FileAsync(sources: sources);

        report.Error.Should().BeNull();
        report.Notes.Should().BeEmpty("a write that worked says nothing");
        sources.Writes.Should().ContainSingle().Which.Branch.Should().Be(Branch,
            "the branch is composed from the ticket the filing just created");
    }

    [Fact]
    public async Task Filing_WritesManifestAndPlannedSpecs()
    {
        var sources = new RecordingBranchSources();
        var store = ApprovedSetDoubles.Store();

        await FileAsync(sources: sources, store: store, phases: [Draft("p9000a"), Draft("p9000b")]);

        var (first, second) = await MembersAsync(store);
        var series = (await store.GetAsync("sample-tracker", Key, default))!.Set.Series!;
        sources.Writes[0].Paths.Should().Contain(
        [
            SeriesPaths.Manifest(series),
            $"{Planned}/{first}-do-the-thing.yaml",
            $"{Planned}/{second}-do-the-thing.yaml",
        ]).And.NotContain(p => p.Contains(Key), "nothing lies under a ticket directory");
        sources.Writes[0].ContentOf($"{Planned}/{first}-do-the-thing.yaml").Should()
            .Contain($"spec: {first}", "the schema-valid yaml is what the run reads back");
        new SeriesManifest().Parse(sources.Writes[0].ContentOf(SeriesPaths.Manifest(series)))!.Ticket
            .Should().Be(Key, "the run finds the manifest by the ticket key it names");
    }

    /// <summary>
    /// 2026-10-06-03c7f: an epic's parent goal is the series' goal — stored with the approval and
    /// written into the manifest beside the approval the row holds.
    /// </summary>
    [Fact]
    public async Task Filing_Epic_ManifestCarriesGoalAndApproval()
    {
        var sources = new RecordingBranchSources();
        var store = ApprovedSetDoubles.Store();
        var parent = new PhaseDraft("p9000", "Move the billing stack", "spec: p9000", []);

        await FileAsync(sources: sources, store: store,
            phases: [Draft("p9000a"), Draft("p9000b")], parent: parent);

        var record = (await store.GetAsync("sample-tracker", Key, default))!;
        record.Set.Goal.Should().Be("Move the billing stack", "the row keeps the epic's goal");
        var manifest = new SeriesManifest().Parse(
            sources.Writes[0].ContentOf(SeriesPaths.Manifest(record.Set.Series!)))!;
        manifest.Goal.Should().Be("Move the billing stack");
        new SeriesManifest().ApprovalOf(manifest).Should().Be(record.Approval,
            "the manifest's approval is the row's");
    }

    /// <summary>
    /// The first run's publish is only a no-op if it finds the directory it would have written
    /// itself. A filing that wrote a SUBSET would leave that run staging the rest and committing a
    /// second revision over a set nobody changed — so both writers render through one place, and
    /// this pins that they agree file for file.
    /// </summary>
    [Fact]
    public async Task Filing_TheBranchItWrites_CarriesEveryFileTheRunsOwnPublishWouldWrite()
    {
        var sources = new RecordingBranchSources();
        var store = ApprovedSetDoubles.Store();

        await FileAsync(sources: sources, store: store, phases: [Draft("p9000a"), Draft("p9000b")]);

        var written = sources.Writes[0];
        var record = (await store.GetAsync("sample-tracker", Key, default))!;
        var published = Published(record, written);
        var expected = new SeriesFiles(new SeriesManifest()).Render(published);
        written.Paths.Should().BeEquivalentTo(expected.Select(f => f.Path));
        written.ContentOf($"{Planned}/{(await MembersAsync(store)).First}-do-the-thing.md").Should().NotBeNull(
            "the markdown companion is part of every cut too");
    }

    [Fact]
    public async Task Filing_TheIndexItWrites_NamesRevisionOneWithTheApprovalAsItsCause()
    {
        var sources = new RecordingBranchSources();

        await FileAsync(sources: sources);

        var doc = Index(sources);
        doc.Revisions.Should().ContainSingle();
        doc.Revisions[0].Number.Should().Be(1,
            "an index with no revisions reads back as revision 1 'initial derivation', which is "
            + "the one thing an approved set is not");
        doc.Revisions[0].Cause.Should().Contain(SpecRevisionCause.Approval).And.Contain("job-1",
            "the cause names the conversation the approval was given in");
    }

    [Fact]
    public async Task Filing_TheIndexItWrites_CarriesTheApprovalInstantTheRecordCarries()
    {
        var sources = new RecordingBranchSources();
        var store = ApprovedSetDoubles.Store();

        await FileAsync(sources: sources, store: store);

        var record = (await store.GetAsync("sample-tracker", Key, default))!;
        new SeriesManifest().ApprovalOf(Index(sources))!.At.Should().Be(record.Approval!.At,
            "the run reads who approved this and when out of the branch, so the store is not "
            + "needed for the approval fact at all");
    }

    /// <summary>
    /// 2026-09-22-6ad7 assumption: a set written at FILING time carries its whole approval,
    /// because the one writer serializes it through the same index the run's reader parses back.
    /// The instant alone is not enough — the conversation and the principal are what the branch
    /// has to carry once the record stops being a source.
    /// </summary>
    [Fact]
    public async Task Filing_TheIndexItWrites_RoundTripsTheWholeApprovalTheRecorderMinted()
    {
        var sources = new RecordingBranchSources();
        var store = ApprovedSetDoubles.Store();

        await FileAsync(sources: sources, store: store);

        var record = (await store.GetAsync("sample-tracker", Key, default))!;
        new SeriesManifest().ApprovalOf(Index(sources)).Should().Be(record.Approval,
            "instant, conversation and principal all survive Serialize and ApprovalOf");
    }

    /// <summary>
    /// 2026-09-22-6ad7 assumption: the run checks out the SAME branch the filing wrote to, so the
    /// specs are in the working tree the reader reads. Both compose it from the ticket id.
    /// </summary>
    [Fact]
    public async Task Filing_TheBranchItWrites_IsTheBranchTheRunChecksOut()
    {
        var sources = new RecordingBranchSources();

        await FileAsync(sources: sources);

        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.TicketId, new TicketId("1"));
        sources.Writes[0].Branch.Should().Be(
            RunBranchResolver.Resolve(pipeline)!.Name.Value,
            "a run that landed on a different branch would never see what filing wrote");
    }

    /// <summary>
    /// A set that never runs the model can only acquire a fingerprint here: the refresh fires when
    /// the model ran, when there was no previous set, or when an edit was seen — and writing the
    /// branch at filing makes "no previous set" false from the first run onward. Without this the
    /// kept-edit notice would go silent for every ticket filed this way, permanently.
    /// <para>
    /// It is taken over the ticket AS THE TRACKER STORED IT, because that is what a later run
    /// fetches; a fingerprint over the body we rendered would differ from the very next read on
    /// any tracker that round-trips markup.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Filing_TheIndexItWrites_CarriesTheFingerprintOfTheTicketTheTrackerStored()
    {
        var sources = new RecordingBranchSources();
        var provider = new JournallingProvider();

        await FileAsync(sources: sources, provider: provider);

        new SeriesManifest().FingerprintOf(Index(sources)).Should()
            .Be(TicketTextFingerprint.Of(provider.Stored), "the run compares against this text")
            .And.NotBe(TicketTextFingerprint.Of(RenderedAsSent(provider)),
                "a fingerprint over the body we SENT would report an edit on the first run");
    }

    /// <summary>
    /// A spec-path commit whose sha is not the one this system recorded is read as a REVIEWER'S
    /// EDIT, and an absent pointer reads the same way — so without the pointer the very first run
    /// would report the operator's own approval as a stranger's correction.
    /// </summary>
    [Fact]
    public async Task Filing_AfterTheWrite_ThePointerNamesTheShaAndTheRepositoryItWroteInto()
    {
        var sources = new RecordingBranchSources();
        var pointers = new InMemorySpecSetPointerStore();

        await FileAsync(sources: sources, pointers: pointers);

        var pointer = await pointers.GetAsync("proj", Key, default);
        pointer.Should().NotBeNull();
        pointer!.RevisionSha.Should().Be(RecordingBranchSources.DefaultSha);
        pointer.RevisionNumber.Should().Be(1);
        pointer.CarryingRepo.Should().Be("sample-api",
            "the pointer's carrying repo is also what makes the run resolve the repository "
            + "this write chose");
    }

    /// <summary>
    /// Before the ticket there is no branch name to compose; after the start there is a run racing
    /// the write. The gap between the record and the start is the only place it fits.
    /// </summary>
    [Fact]
    public async Task Filing_TheWriteHappens_AfterTheRecordAndBeforeTheTicketIsStarted()
    {
        var journal = new List<string>();
        var provider = new JournallingProvider(journal);
        var sources = new RecordingBranchSources { OnWrite = () => journal.Add("branch written") };
        var store = new JournallingStore(journal);

        await FileAsync(
            sources: sources, store: store, provider: provider, config: Routing(),
            starter: FiledWorkDoubles.Starter(Routing()), mayStartRuns: true);

        journal.Should().Equal(
            "ticket created", "set stored", "branch written", "ticket tagged", "ticket started");
    }

    [Fact]
    public async Task Filing_ABranchWriteThatThrows_IsANoteAndNotAFilingError()
    {
        var sources = new RecordingBranchSources { Throws = new HttpRequestException("403 from the remote") };

        var report = await FileAsync(sources: sources);

        report.Error.Should().BeNull(
            "an error would tell the operator to retry, and the retry files a SECOND ticket");
        report.Notes.Should().ContainSingle().Which.Should()
            .Contain("https://tracker.test/1").And.Contain("403 from the remote")
            .And.Contain("publishes the set to the branch from the stored record");
    }

    [Fact]
    public async Task Filing_ABranchWriteThatThrows_StillReportsTheTicketAsFiled()
    {
        var sources = new RecordingBranchSources { Throws = new HttpRequestException("the remote is down") };
        var store = ApprovedSetDoubles.Store();

        var report = await FileAsync(sources: sources, store: store);

        report.Succeeded.Should().BeTrue();
        report.Filed.Should().ContainSingle().Which.Reference.Should().Be("https://tracker.test/1");
        (await store.GetAsync("sample-tracker", Key, default)).Should().NotBeNull(
            "the ticket and its approved set are complete — only the branch is not written");
    }

    /// <summary>A remote that accepts the files but names no commit is the contract's failure
    /// case: without a sha there is no pointer, and an absent pointer is read as a foreign edit.</summary>
    [Fact]
    public async Task Filing_AWriteThatNamesNoCommit_IsANoteAndRecordsNoPointer()
    {
        var sources = new RecordingBranchSources { Sha = null };
        var pointers = new InMemorySpecSetPointerStore();

        var report = await FileAsync(sources: sources, pointers: pointers);

        report.Notes.Should().ContainSingle().Which.Should().Contain("named no commit");
        (await pointers.GetAsync("proj", Key, default)).Should().BeNull();
    }

    [Fact]
    public async Task Filing_ARepositoryHandedToIt_IsTheOneTheBranchIsCutIn()
    {
        var sources = new RecordingBranchSources();
        var handed = new RepoConnection { Name = "sample-web" };

        var result = await ApprovedSetDoubles.Branch(sources).WriteAsync(
            Project(), Record(carrier: "sample-api"), new TicketId("1"), Stored(), handed, default);

        result.Written.Should().BeTrue(result.Error);
        sources.Writes[0].Repo.Should().Be("sample-web",
            "the repository is an INPUT; a sibling phase gives the conversation that choice");
    }

    [Fact]
    public async Task Filing_NoRepositoryChosen_UsesTheFirstTheApprovalNamed()
    {
        var sources = new RecordingBranchSources();

        await ApprovedSetDoubles.Branch(sources).WriteAsync(
            Project(), Record(carrier: string.Empty, repositories: ["sample-web"]),
            new TicketId("1"), Stored(), carrier: null, default);

        sources.Writes[0].Repo.Should().Be("sample-web",
            "the fallback is the run's own rule — the first scoped repository, not the first "
            + "configured one");
    }

    /// <summary>
    /// The record carries the carrier AFTER the tracker name because the sole construction site is
    /// positional and both are strings: inserted earlier it would compile in silence and write the
    /// tracker name as the carrier.
    /// </summary>
    [Fact]
    public async Task ApprovalRecord_TheCarrier_IsTheFirstConfiguredRepositoryTheApprovalNamed()
    {
        var store = ApprovedSetDoubles.Store();

        await FileAsync(store: store, scoped: ["sample-web", "sample-api"]);

        var record = (await store.GetAsync("sample-tracker", Key, default))!;
        record.Tracker.Should().Be("sample-tracker");
        record.CarryingRepo.Should().Be("sample-api",
            "the carrier is the first of the PROJECT's repositories the approval named, which is "
            + "the first element of the scoped list a run of that approval resolves");
    }

    [Fact]
    public void Run_TheCarrierTheApprovalChose_OutranksTheFirstScopedRepoAndYieldsToThePointer()
    {
        IReadOnlyList<RepoConnection> scoped =
            [new RepoConnection { Name = "sample-api" }, new RepoConnection { Name = "sample-web" }];

        SpecCarryingRepoResolver.Resolve(scoped, pointer: null, approvedCarrier: "sample-web")!
            .Name.Should().Be("sample-web");
        SpecCarryingRepoResolver.Resolve(
            scoped, new SpecSetPointer(Key, "sample-api", "sha", 2), approvedCarrier: "sample-web")!
            .Name.Should().Be("sample-api", "the pointer is where this system last committed");
    }

    /// <summary>
    /// A branch whose set carries no fingerprint can never acquire one, so it is not written at
    /// all: the filing is complete either way, and the run's own publish refreshes the fingerprint
    /// because it finds no previous set.
    /// </summary>
    [Fact]
    public async Task Filing_ATicketThatCannotBeReadBack_WritesNoBranchAndSaysWhy()
    {
        var sources = new RecordingBranchSources();

        var report = await FileAsync(sources: sources, provider: new JournallingProvider { ReadBackFails = true });

        sources.Writes.Should().BeEmpty();
        report.Succeeded.Should().BeTrue();
        report.Notes.Should().ContainSingle().Which.Should().Contain("fingerprint");
    }

    /// <summary>
    /// The run's own reader is the consumer, so what filing wrote has to read back through it —
    /// as an approved set at revision 1, carrying the approval the record carried.
    /// </summary>
    [Fact]
    public async Task SeriesReader_TheSetFilingWrote_ReadsBackAsAnApprovedSetAtRevisionOne()
    {
        var sources = new RecordingBranchSources();
        var store = ApprovedSetDoubles.Store();
        await FileAsync(sources: sources, store: store, phases: [Draft("p9000a"), Draft("p9000b")]);
        var branch = new SpecBranchFiles { Key = Key };
        foreach (var file in sources.Writes[0].Files) branch.Seed(file.Path, file.Content);

        var read = await ReadAsync(branch);

        read.Should().NotBeNull("filing wrote a directory the run's own reader can open");
        var (first, second) = await MembersAsync(store);
        read!.Set.Phases.Select(p => p.PhaseId).Should().Equal(first, second);
        read.Set.Series.Should().Be((await store.GetAsync("sample-tracker", Key, default))!.Set.Series,
            "the branch carries the series the filing minted");
        read.Set.Current.Number.Should().Be(1);
        read.Set.Current.Cause.Should().Contain(SpecRevisionCause.Approval);
        read.Set.Approval!.At.Should().Be(
            (await store.GetAsync("sample-tracker", Key, default))!.Approval!.At);
        read.Set.TicketFingerprint.Should().NotBeNullOrWhiteSpace();
    }

    private static async Task<SpecSetReadResult?> ReadAsync(SpecBranchFiles branch)
    {
        var sandbox = new Mock<ISandbox>();
        sandbox.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepResult(1, Guid.Empty, 0, false, 0, null, "branch-sha"));
        var readers = new Mock<ISandboxFileReaderFactory>();
        readers.Setup(f => f.Create(It.IsAny<ISandbox>())).Returns(branch);
        var pipeline = new PipelineContext();
        pipeline.Set<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, new Dictionary<string, ISandbox> { ["sample-api"] = sandbox.Object });
        var reader = SeriesDoubles.Reader(readers.Object);
        var onBranch = await reader.ReadAsync(
            pipeline, new RepoConnection { Name = "sample-api" }, new TicketKey(Key), default);
        return onBranch.Read;
    }

    // 2026-10-06-03c7c: the ids filing minted for a two-slice cut — its series' base plus a letter.
    private static async Task<(string First, string Second)> MembersAsync(ISpecApprovalStore store)
    {
        var series = (await store.GetAsync("sample-tracker", Key, default))!.Set.Series!;
        return (SeriesIdFactory.Member(series, 0), SeriesIdFactory.Member(series, 1));
    }

    /// <summary>
    /// 2026-10-06-03c7e: filing never reads the branch, so the pointer tells it how many leading
    /// specs the record step already moved to done/ — an amendment must not write one back.
    /// </summary>
    [Fact]
    public async Task Amendment_AfterExecutedSpec_WritesNoPlannedFileForIt()
    {
        var sources = new RecordingBranchSources();
        var pointers = new InMemorySpecSetPointerStore();
        await pointers.SaveAsync("proj", new SpecSetPointer(Key, "sample-api", "record-sha", 1, ExecutedThrough: 1), default);
        var amended = Record("sample-api") with
        {
            Set = Record("sample-api").Set with
            {
                Phases = [new SpecPhase(Draft("2026-10-06-3d3da"), "first", string.Empty, []),
                    new SpecPhase(Draft("2026-10-06-3d3db"), "second", string.Empty, [])],
            },
        };

        var result = await ApprovedSetDoubles.Branch(sources, pointers).WriteAsync(
            Project(), amended, new TicketId("1"), Stored(), carrier: null, default);

        result.Written.Should().BeTrue(result.Error);
        sources.Writes[0].Paths.Should().Contain($"{Planned}/2026-10-06-3d3db-second.yaml")
            .And.NotContain(p => p.Contains("2026-10-06-3d3da"), "the executed spec lies in done/ and stays there");
        Index(sources).Specs.Should().Equal(["2026-10-06-3d3da", "2026-10-06-3d3db"], "the series keeps its order");
        (await pointers.GetAsync("proj", Key, default))!.ExecutedThrough.Should().Be(1, "filing keeps the count");
    }

    private static SeriesManifestDocument Index(RecordingBranchSources sources) =>
        new SeriesManifest().Parse(sources.Writes[0].ContentOf(ManifestPath(sources.Writes[0])))!;

    private static string ManifestPath(RecordingBranchSources.BranchWrite written) =>
        written.Paths.Single(p => p.StartsWith(SeriesPaths.SeriesRoot + "/", StringComparison.Ordinal));

    // What the branch carries: the stored set plus the revision and fingerprint filing mints.
    private static SpecSet Published(SpecApprovalRecord record, RecordingBranchSources.BranchWrite written)
    {
        var doc = new SeriesManifest().Parse(written.ContentOf(ManifestPath(written)))!;
        return record.Set with
        {
            Revisions = [new SpecRevision(
                doc.Revisions[0].Number, doc.Revisions[0].Cause, DateTimeOffset.Parse(doc.Revisions[0].At))],
            TicketFingerprint = doc.TicketFingerprint,
        };
    }

    private static Task<FilingReport> FileAsync(
        RecordingBranchSources? sources = null, ISpecApprovalStore? store = null,
        ISpecSetPointerStore? pointers = null, JournallingProvider? provider = null,
        FiledWorkStarter? starter = null, bool mayStartRuns = false,
        IReadOnlyList<PhaseDraft>? phases = null, IReadOnlyList<string>? scoped = null,
        AgentSmithConfig? config = null, PhaseDraft? parent = null)
    {
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>()))
            .Returns(provider ?? new JournallingProvider());
        var filer = new OutcomeTicketFiler(
            config ?? Config(), factory.Object, new PhaseTicketRenderer(), new BugTicketRenderer(),
            new EpicChildOrderer(), ApprovedSetDoubles.SeriesFiling(),
            ApprovedSetDoubles.SetFiler(store, starter, sources ?? new RecordingBranchSources(), pointers),
            FiledWorkDoubles.Starter(), ApprovedSetDoubles.Kinds(),
            NullLogger<OutcomeTicketFiler>.Instance);
        var set = phases ?? [Draft("p9000a")];
        OutcomeProposal proposal = set.Count == 1
            ? new PhaseOutcome(set[0])
            : new EpicOutcome(parent ?? Draft("p9000"), set);
        return filer.FileAsync(State(scoped), proposal, mayStartRuns, CancellationToken.None);
    }

    private static Ticket Stored() =>
        new(new TicketId("1"), StoredTitle, StoredBody, null, "open", "journalling", []);

    private static Ticket RenderedAsSent(JournallingProvider provider) =>
        new(new TicketId("1"), provider.Created[0].Title, provider.Created[0].Body, null, "open",
            "journalling", []);

    private static SpecApprovalRecord Record(
        string carrier, IReadOnlyList<string>? repositories = null) =>
        new(Key,
            new SpecSet(
                Key, [new SpecPhase(Draft("p9000a"), "do-the-thing", string.Empty, [])],
                SpecAccounting.Empty, [], SpecSource.Approved,
                Approval: new SpecApproval(DateTimeOffset.UnixEpoch, "job-1", "sample.user"),
                Series: "2026-10-06-3d3d"),
            repositories ?? ["sample-api", "sample-web"], "sample-tracker", carrier);

    private static PhaseDraft Draft(string id) =>
        new(id, "Do the thing", $"spec: {id}\ngoal: \"Do the thing\"", [])
        { Done = ["It is done."] };

    private static AgentSmithConfig Config() => new()
    {
        Projects = new Dictionary<string, ResolvedProject> { ["proj"] = Project() },
    };

    private static ResolvedProject Project() => new()
    {
        Name = "proj",
        Tracker = new TrackerConnection { Name = "sample-tracker", Type = TrackerType.AzureDevOps },
        Repos = [new RepoConnection { Name = "sample-api" }, new RepoConnection { Name = "sample-web" }],
    };

    // A routing config the filed ticket actually resolves against, so the START is a real move.
    private static AgentSmithConfig Routing() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>(StringComparer.Ordinal)
        {
            ["proj"] = Project() with
            {
                DefaultPipeline = "code",
                AzuredevopsTrigger = new WebhookTriggerConfig
                {
                    DefaultPipeline = "code",
                    TriggerStatuses = ["To Do"],
                    ProjectResolution = new ProjectResolutionConfig
                    {
                        Strategy = ResolutionStrategy.Tag, Value = "widgets",
                    },
                },
            },
        },
    };

    private static ConversationState State(IReadOnlyList<string>? scoped) => new()
    {
        JobId = "job-1",
        ChannelId = "C1",
        UserId = "sample.user",
        Platform = "dashboard",
        Project = "proj",
        TicketId = string.Empty,
        StartedAt = DateTimeOffset.UnixEpoch,
        Scope = scoped is null ? null : new ActiveScope { Project = "proj", Repos = scoped },
    };

    /// <summary>
    /// A tracker that records WHEN each of its calls happened, and whose read-back answers text
    /// the filing never sent — the markup round-trip a real tracker performs.
    /// </summary>
    private sealed class JournallingProvider(List<string>? journal = null) : ITicketProvider
    {
        private readonly List<(string Title, string Body)> _created = [];

        internal IReadOnlyList<(string Title, string Body)> Created => _created;

        internal bool ReadBackFails { get; init; }

        internal Ticket Stored { get; } =
            new(new TicketId("1"), StoredTitle, StoredBody, null, "open", "journalling", []);

        public string ProviderType => "journalling";

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ConnectionProbeResult.Reachable(0));

        // NOT journalled: the starter reads the ticket again to see its status, and the order
        // this test pins is the order of the four acts a filing PERFORMS.
        public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken) =>
            ReadBackFails
                ? throw new InvalidOperationException("the tracker is not answering")
                : Task.FromResult(Stored);

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels, string? kind,
            CancellationToken cancellationToken)
        {
            journal?.Add("ticket created");
            _created.Add((title, description));
            return Task.FromResult(new CreatedTicket(
                new TicketId(_created.Count.ToString()), $"https://tracker.test/{_created.Count}"));
        }

        public Task<bool> AddLabelAsync(TicketId ticketId, string label, CancellationToken cancellationToken)
        {
            journal?.Add("ticket tagged");
            return Task.FromResult(true);
        }

        public Task<bool> TransitionToAsync(
            TicketId ticketId, string statusName, CancellationToken cancellationToken)
        {
            journal?.Add("ticket started");
            return Task.FromResult(true);
        }

        public Task<TicketFinalizeResult> FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken) =>
            Task.FromResult(TicketFinalizeResult.Moved());
    }

    private sealed class JournallingStore(List<string> journal) : ISpecApprovalStore
    {
        private readonly InMemorySpecApprovalStore _inner = new();

        public Task<SpecApprovalRecord?> GetAsync(
            string tracker, string key, CancellationToken cancellationToken) =>
            _inner.GetAsync(tracker, key, cancellationToken);

        public Task SaveAsync(SpecApprovalRecord record, CancellationToken cancellationToken)
        {
            journal.Add("set stored");
            return _inner.SaveAsync(record, cancellationToken);
        }

        public Task<OutstandingApprovals> ListOutstandingAsync(
            string tracker, int limit, CancellationToken cancellationToken) =>
            Task.FromResult(OutstandingApprovals.None);

        public Task MarkSatisfiedAsync(
            string tracker, string key, DateTimeOffset at, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ReopenAsync(string tracker, string key, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
