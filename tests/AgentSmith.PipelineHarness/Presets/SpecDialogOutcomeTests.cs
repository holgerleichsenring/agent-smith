using System.Runtime.CompilerServices;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0315e/p0315c: typed outcome resolution + ticket filing end-to-end through
/// the REAL server composition. The turn runner resolves the master's
/// terminal output into an OutcomeProposal; SpecDialogOutcomeFlow confirms it
/// in-thread — the question renders through the platform adapter's generic
/// approval blocks (p0058 surface) while a text reply travels the dialogue
/// transport — and only a confirmed proposal reaches the sink. The CreatePhase_*
/// tests run the REAL TicketFilingOutcomeSink over a migrated SQLite session
/// store with a recording ITicketProvider at the tracker-HTTP boundary; the
/// resolution tests keep a recording sink at the p0315c seam.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed partial class SpecDialogOutcomeTests
{
    private const string Project = "fixture-spec-dialog";
    private const string Repo = "spec-dialog-fixture";

    private const string ValidDraftYaml =
        """
        phase: p9999
        goal: "Add a widget endpoint to the sample service"
        steps:
          - id: impl
            action: "Add the widget endpoint + handler"
        tests:
          - "Widget_Get_ReturnsWidget"
        done:
          - "GET /widget returns the widget"
        """;

    private const string ValidDraft =
        $"""
        ```yaml
        {ValidDraftYaml}
        ```
        """;

    private const string BugOutcomeReply =
        """
        That is a one-line fix, not a phase.
        ```outcome
        kind: bug
        title: "Add a null check to AppendTurnAsync"
        description: "AppendTurnAsync dereferences the session state without a null check; return early when the thread has no open session."
        ```
        """;

    private const string EpicOutcomeReply =
        """
        This exceeds one phase — proposing an epic.
        ```outcome
        kind: epic
        parent:
          phase: p9000
          goal: "Widget platform end to end"
        children:
          - phase: p9000a
            goal: "Widget storage layer"
            steps:
              - id: store
                action: "Add the widget store"
            done:
              - "a widget is stored and read back"
          - phase: p9000b
            goal: "Widget API on top of the storage layer"
            requires: [p9000a]
            steps:
              - id: api
                action: "Add the widget endpoint"
            done:
              - "the endpoint returns a stored widget"
        ```
        """;

    // ---- p0315e: resolution + confirmation (recording sink at the seam) ----

    [Fact]
    public async Task Outcome_ExplainQuestion_ResolvesAnswer_NoArtifact()
    {
        var (bridge, adapter, sink) =
            (new InMemoryDialogueBridge(), new RecordingChatAdapter(), new RecordingOutcomeSink());
        await using var harness = BuildHarness(bridge, adapter, ReplaceSink(sink));
        const string answer = "Dispatch flows through the intent engine into the per-intent handlers.";
        harness.ChatClient.EnqueueText(answer);
        var state = State("how does message dispatch work?");

        var result = await RunTurnAsync(harness, state);
        await RunFlowAsync(harness, state, result.Outcome);

        result.Outcome.Should().BeOfType<AnswerOutcome>();
        result.Reply.Should().Be(answer).And.NotContain("```", "an answer carries no artifact");
        adapter.Questions.Should().BeEmpty("an answer needs no confirmation");
        sink.Accepted.Should().BeEmpty("an answer routes nowhere");
    }

    [Fact]
    public async Task Outcome_SmallChange_ResolvesBug_RoutesFixBug()
    {
        var (bridge, adapter, sink) = (new InMemoryDialogueBridge(),
            new RecordingChatAdapter { AutoAnswer = "approve" }, new RecordingOutcomeSink());
        await using var harness = BuildHarness(bridge, adapter, ReplaceSink(sink));
        harness.ChatClient.EnqueueText(BugOutcomeReply);
        var state = Discussed("add a null check to AppendTurnAsync");

        var result = await RunTurnAsync(harness, state);
        await RunFlowAsync(harness, state, result.Outcome);

        var bug = result.Outcome.Should().BeOfType<BugOutcome>().Subject;
        bug.Ticket.Title.Should().Be("Add a null check to AppendTurnAsync");
        bug.Ticket.Description.Should().Contain("null check");
        sink.Accepted.Should().ContainSingle().Which.Should().BeOfType<BugOutcome>(
            "the confirmed fix-bug ticket shape is what routes down the fix-bug path");
    }

    [Fact]
    public async Task Outcome_Feature_ResolvesSinglePhase()
    {
        var (bridge, adapter, sink) = (new InMemoryDialogueBridge(),
            new RecordingChatAdapter { AutoAnswer = "approve" }, new RecordingOutcomeSink());
        await using var harness = BuildHarness(bridge, adapter, ReplaceSink(sink));
        harness.ChatClient.EnqueueText($"Here is the phase draft:\n{ValidDraft}");
        var state = Discussed("draft the widget phase now");

        var result = await RunTurnAsync(harness, state);
        await RunFlowAsync(harness, state, result.Outcome);

        var phase = result.Outcome.Should().BeOfType<PhaseOutcome>().Subject;
        phase.Draft.PhaseId.Should().Be("p9999");
        phase.Draft.Yaml.Should().Contain("goal:", "the full schema-valid spec travels with the proposal");
        sink.Accepted.Should().ContainSingle().Which.Should().BeOfType<PhaseOutcome>();
    }

    [Fact]
    public async Task Runner_OnTheDashboard_KeepsTheDraftAndShowsTheProseWithoutIt()
    {
        var (bridge, adapter, sink) =
            (new InMemoryDialogueBridge(), new RecordingChatAdapter(), new RecordingOutcomeSink());
        await using var harness = BuildHarness(bridge, adapter, ReplaceSink(sink));
        harness.ChatClient.EnqueueText($"Here is the phase draft:\n{ValidDraft}");

        var result = await RunTurnAsync(harness, Discussed("draft the widget phase now") with { Platform = "dashboard" });

        result.Outcome.Should().BeOfType<PhaseOutcome>();
        result.Reply.Should().Contain("```yaml", "the transcript keeps what the master wrote");
        result.Shown.Should().Be("Here is the phase draft:", "the dashboard pane shows the draft");
    }

    [Fact]
    public async Task Outcome_LargeFeature_ProposesEpicWithLinkedRequires()
    {
        var (bridge, adapter, sink) = (new InMemoryDialogueBridge(),
            new RecordingChatAdapter { AutoAnswer = "approve" }, new RecordingOutcomeSink());
        await using var harness = BuildHarness(bridge, adapter, ReplaceSink(sink));
        harness.ChatClient.EnqueueText(EpicOutcomeReply);
        var state = Discussed("build the whole widget platform");

        var result = await RunTurnAsync(harness, state);
        await RunFlowAsync(harness, state, result.Outcome);

        var epic = result.Outcome.Should().BeOfType<EpicOutcome>().Subject;
        epic.Parent.PhaseId.Should().Be("p9000");
        epic.Children.Select(c => c.PhaseId).Should().Equal("p9000a", "p9000b");
        epic.Children[1].Requires.Should().ContainSingle("the slices are linked by requires: edges")
            .Which.Should().Be("p9000a");
        adapter.Questions.Should().ContainSingle().Which.Text.Should()
            .Contain("p9000a").And.Contain("requires: p9000a",
                "the epic slice shape is shown in-thread before anything is filed");
        sink.Accepted.Should().ContainSingle().Which.Should().BeOfType<EpicOutcome>();
    }

    [Fact]
    public async Task Outcome_Proposed_ConfirmedBeforeFiling()
    {
        var (bridge, adapter, sink) =
            (new InMemoryDialogueBridge(), new RecordingChatAdapter(), new RecordingOutcomeSink());
        await using var harness = BuildHarness(bridge, adapter, ReplaceSink(sink));
        harness.ChatClient.EnqueueText($"Draft:\n{ValidDraft}");
        var state = Discussed("draft the widget phase now");

        var result = await RunTurnAsync(harness, state);
        var flow = RunFlowAsync(harness, state, result.Outcome);
        var question = await adapter.WaitForQuestionAsync();

        sink.Accepted.Should().BeEmpty("nothing routes before the explicit in-thread confirmation");
        question.Type.Should().Be(QuestionType.Approval);
        question.Text.Should().Contain("p9999", "the proposed outcome is shown for confirmation");

        // The operator answers by TEXT in the thread — the dialogue-transport
        // path — not by button; both surfaces feed the same wait.
        await bridge.PublishAnswerAsync(state.JobId,
            new DialogAnswer(question.QuestionId, "approve", null, DateTimeOffset.UtcNow, "U-harness"),
            CancellationToken.None);
        (await flow).Should().BeOfType<OutcomeFlowCompleted>();

        sink.Accepted.Should().ContainSingle("the approval releases the proposal to the sink");
    }

    // ---- p0315c: /create-phase files confirmed outcomes as real tickets ----

    [Fact]
    public async Task CreatePhase_AnswerOutcome_FilesNothing()
    {
        await using var bed = await FilingBed.BuildAsync(autoAnswer: "approve");
        var state = await bed.OpenSessionAsync("th-answer");

        var flowResult = await RunFlowAsync(bed.Harness, state, new AnswerOutcome());

        flowResult.Should().BeOfType<OutcomeFlowCompleted>();
        bed.Tickets.Created.Should().BeEmpty("an answer files nothing");
        bed.Adapter.Questions.Should().BeEmpty("an answer is never proposed for confirmation");
    }

    [Fact]
    public async Task CreatePhase_BugOutcome_FilesFixBugTicket()
    {
        await using var bed = await FilingBed.BuildAsync(autoAnswer: "approve");
        var state = await bed.OpenSessionAsync("th-bug");
        var bug = new BugOutcome(new BugTicketDraft(
            "Add a null check to AppendTurnAsync",
            "AppendTurnAsync dereferences the session state without a null check.",
            "Returns early when the thread has no open session."));

        await RunFlowAsync(bed.Harness, state, bug);

        var created = bed.Tickets.Created.Should().ContainSingle().Subject;
        created.Title.Should().Be("Add a null check to AppendTurnAsync");
        created.Body.Should().Contain("without a null check")
            .And.Contain("## Acceptance criteria").And.Contain("Returns early");
        created.Labels.Should().BeEmpty(
            "the fix-bug shape mirrors the existing create-ticket path: title + body, no phase label");
        bed.Adapter.SentTexts.Should().Contain(t => t.Contains("https://tracker.test/1"),
            "the ticket URL is posted back to the thread");
        (await bed.TrailAsync("th-bug")).Should().Contain(t => t.Contains("https://tracker.test/1"),
            "the ticket URL lands in the dialogue trail");
    }

    [Fact]
    public async Task CreatePhase_PhaseOutcome_FilesOneRequirementAndStoresTheApprovedSet()
    {
        await using var bed = await FilingBed.BuildAsync(autoAnswer: "approve");
        var state = await bed.OpenSessionAsync("th-phase");
        var draft = new PhaseDraft(
            "p9999", "Add a widget endpoint to the sample service", ValidDraftYaml, []);

        await RunFlowAsync(bed.Harness, state, new PhaseOutcome(draft));

        var created = bed.Tickets.Created.Should().ContainSingle().Subject;
        created.Title.Should().Be("p9999: Add a widget endpoint to the sample service");
        created.Labels.Should().Equal(FiledTicketLabels.ApprovedSetStamp);
        // 2026-09-17-0e79a: the spec is the approved RECORD, stored under the created ticket's
        // spec key; the body carries no fence for anyone with tracker access to edit.
        created.Body.Should().NotContain("```");
        // The record is identified by the tracker CONNECTION and the spec key — the same pair
        // SpecSetKeyFactory and ExecutePipelineUseCase hand the run.
        var tracker = bed.Harness.Services.GetRequiredService<AgentSmithConfig>()
            .Projects[Project].Tracker;
        var record = await bed.Harness.Services.GetRequiredService<ISpecApprovalStore>()
            .GetAsync(tracker.Name, SpecSetKey.For(tracker.Type.ToString().ToLowerInvariant(), "1").Value,
                CancellationToken.None);
        record.Should().NotBeNull("the run that works this ticket resolves the set by exactly this key");
        record!.Set.Phases.Should().ContainSingle().Which.Draft.Yaml.Should().Be(ValidDraftYaml.Trim(),
            "the stored set carries the schema-valid spec verbatim");
        record.Approval.Should().NotBeNull();
        bed.Adapter.SentTexts.Should().Contain(t => t.Contains("https://tracker.test/1"));
    }

    /// <summary>
    /// 2026-09-17-0e79d: one work ticket, one run, one pull request per repository.
    /// 2026-09-22-b3d7: and ONE TICKET IN THE TRACKER, whatever the slice count. The records this
    /// preset used to pin — three tickets, two of them carrying only the record label, two parent
    /// links and a comment listing them — were a second copy of the work ticket's own slice list.
    /// </summary>
    [Fact]
    public async Task CreatePhase_EpicOutcome_FilesOneWorkTicketAndNothingBesideIt()
    {
        await using var bed = await FilingBed.BuildAsync(autoAnswer: "approve");
        var state = await bed.OpenSessionAsync("th-epic");
        var epic = new EpicOutcome(
            new PhaseDraft("p9000", "Widget platform end to end",
                "phase: p9000\ngoal: \"Widget platform end to end\"", []),
            [
                new PhaseDraft("p9000a", "Widget storage layer",
                    "phase: p9000a\ngoal: \"Widget storage layer\"\nsteps:\n  - id: store\n    action: \"Add the widget store\"\ndone:\n  - \"a widget is stored and read back\"",
                    []),
                new PhaseDraft("p9000b", "Widget API on top of the storage layer",
                    "phase: p9000b\ngoal: \"Widget API on top of the storage layer\"\nrequires: [p9000a]\nsteps:\n  - id: api\n    action: \"Add the widget endpoint\"",
                    ["p9000a"]),
            ]);

        await RunFlowAsync(bed.Harness, state, epic);

        var work = bed.Tickets.Created.Should().ContainSingle(
            "2026-09-22-b3d7: a cut is one piece of work and files one ticket").Subject;
        work.Title.Should().Be("p9000: Widget platform end to end");
        // 2026-09-17-0e79d: it is what a run picks up, and the set it works is stored under its
        // own spec key. 2026-09-22-b3d7: no ticket the framework files carries the record label.
        work.Labels.Should().Equal(FiledTicketLabels.ApprovedSetStamp);
        bed.Tickets.Created.SelectMany(t => t.Labels).Should()
            .NotContain(PhaseTicketRenderer.EpicLabel);
        bed.Tickets.Links.Should().BeEmpty("nothing is filed under the work ticket to link to it");
        bed.Tickets.Comments.Should().BeEmpty("there are no records to list on it");
        // 2026-09-22-b3d7: the slice list is the ONLY place a person reads a slice on its own, so
        // every id, every goal and every requires: edge has to be in it.
        work.Body.Should().Contain("## Slices")
            .And.Contain("`p9000a` Widget storage layer")
            .And.Contain("`p9000b` Widget API on top of the storage layer (requires: p9000a)");
        work.Body.Should().NotContain("```", "a requirement body opens no fence");
        // 2026-09-17-0e79d: no position stamp — a parent stamp would cut the run's branch from
        // another ticket's rung instead of from its own base.
        FiledTicketLabels.ParentId(work.Labels).Should().BeNull();
        var tracker = bed.Harness.Services.GetRequiredService<AgentSmithConfig>()
            .Projects[Project].Tracker;
        var record = await bed.Harness.Services.GetRequiredService<ISpecApprovalStore>()
            .GetAsync(tracker.Name, SpecSetKey.For(tracker.Type.ToString().ToLowerInvariant(), "1").Value,
                CancellationToken.None);
        record.Should().NotBeNull("the whole approved set is stored under the WORK ticket's key");
        record!.Set.Phases.Select(p => p.PhaseId).Should().Equal("p9000a", "p9000b");
        bed.Adapter.SentTexts.Should().Contain(t => t.Contains("https://tracker.test/1"));
        bed.Adapter.SentTexts.Should().NotContain(t => t.Contains("https://tracker.test/2"),
            "there is no second ticket to name in the thread");
    }

    [Fact]
    public async Task PhaseTicketRenderer_FiledBody_IsAReadableRequirementAndCarriesNoSpec()
    {
        await using var bed = await FilingBed.BuildAsync(autoAnswer: null);
        var draft = new PhaseDraft(
            "p9999", "Add a widget endpoint to the sample service", ValidDraftYaml, []);

        var content = new PhaseTicketRenderer().RenderPhase(draft, "sess-outcome");

        content.Title.Should().Be("p9999: Add a widget endpoint to the sample service");
        content.Body.Should().Contain("## Goal").And.Contain(AcceptanceCriteriaSection.Heading,
            "a person reads what is wanted and what makes it finished");
        content.Body.Should().Contain(PhaseTicketRenderer.SpecificationHeading)
            .And.Contain("sess-outcome", "the body points at the conversation it was approved in");
        var validator = bed.Harness.Services.GetRequiredService<ISpecDraftValidator>();
        validator.Validate(content.Body).Should().BeOfType<SpecDraftAbsent>(
            "a fence in the body would be a second truth the source precedence would take");
    }

    [Fact]
    public async Task CreatePhase_RejectReply_FilesNothingAndSaysSo()
    {
        await using var bed = await FilingBed.BuildAsync(autoAnswer: "reject");
        var state = await bed.OpenSessionAsync("th-reject");
        var draft = new PhaseDraft(
            "p9999", "Add a widget endpoint to the sample service", ValidDraftYaml, []);

        var flowResult = await RunFlowAsync(bed.Harness, state, new PhaseOutcome(draft));

        flowResult.Should().BeOfType<OutcomeFlowCompleted>("a rejection ends the flow — no revision loop");
        bed.Tickets.Created.Should().BeEmpty("reject files nothing");
        bed.Adapter.SentTexts.Should().Contain(t => t.Contains("Rejected — nothing was filed"),
            "the thread is told explicitly");
    }

    [Fact]
    public async Task CreatePhase_EditReply_RequestsRevisionAndFilesNothing()
    {
        await using var bed = await FilingBed.BuildAsync(autoAnswer: "split the storage slice in two");
        var state = await bed.OpenSessionAsync("th-edit");
        var draft = new PhaseDraft(
            "p9999", "Add a widget endpoint to the sample service", ValidDraftYaml, []);

        var flowResult = await RunFlowAsync(bed.Harness, state, new PhaseOutcome(draft));

        flowResult.Should().BeOfType<OutcomeFlowEditRequested>()
            .Which.Note.Should().Be("split the storage slice in two");
        bed.Tickets.Created.Should().BeEmpty("an edit note files nothing — the master revises first");
        bed.Adapter.SentTexts.Should().Contain(t => t.Contains("Revising the proposal"));
    }

    // ---- harness plumbing ----

    private static Action<IServiceCollection> ReplaceSink(RecordingOutcomeSink sink) => services =>
    {
        // The seam under test in the resolution tests: the recording sink
        // observes exactly what a confirmation releases.
        services.RemoveAll<IOutcomeSink>();
        services.AddSingleton<IOutcomeSink>(sink);
    };

    private static RealCompositionHarness BuildHarness(
        InMemoryDialogueBridge bridge, RecordingChatAdapter adapter,
        Action<IServiceCollection>? extra = null) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            // Same boundary overrides as the p0315b SpecDialogTests bed
            // (skills catalog = network, project map = Redis).
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubSkillsCatalogResolver());
            services.RemoveAll<IProjectMapStore>();
            services.AddSingleton<IProjectMapStore>(new CannedProjectMapStore(CannedMap()));
            // Dialogue transport + message bus are Redis streams in production;
            // the bridge is their in-memory pair so text answers reach the wait.
            services.RemoveAll<IDialogueTransport>();
            services.AddSingleton<IDialogueTransport>(bridge);
            services.RemoveAll<IMessageBus>();
            services.AddSingleton<IMessageBus>(bridge);
            // The chat platform HTTP boundary: the recording adapter renders
            // the approval question (blocks surface) and scripts the button.
            services.RemoveAll<IPlatformAdapter>();
            services.AddSingleton<IPlatformAdapter>(adapter);
            extra?.Invoke(services);
        });

    private static async Task<SpecDialogTurnResult> RunTurnAsync(
        RealCompositionHarness harness, ConversationState state)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>();
        return await runner.RunTurnAsync(state, CancellationToken.None);
    }

    private static async Task<OutcomeFlowResult> RunFlowAsync(
        RealCompositionHarness harness, ConversationState state, OutcomeProposal outcome)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var flow = scope.ServiceProvider.GetRequiredService<SpecDialogOutcomeFlow>();
        return await flow.HandleAsync(state, outcome, false, CancellationToken.None);
    }

    // The p0315d inverse: the ticket body must hold exactly ONE ```yaml block
    // and it must be schema-valid — proven by the same validator the draft
    // gate uses.
    private static ConversationState State(string userTurn) => new()
    {
        JobId = "sess-outcome",
        ChannelId = "C-harness",
        UserId = "U-harness",
        Platform = "slack",
        Project = Project,
        TicketId = string.Empty,
        StartedAt = DateTimeOffset.UtcNow,
        Mode = ConversationMode.SpecDialog,
        ThreadId = "th-outcome",
        Transcript = [new TranscriptTurn(TranscriptRole.User, userTurn, DateTimeOffset.UtcNow)],
        Scope = new ActiveScope { Project = Project, Repos = [Repo] },
    };

    // 2026-09-17-042ec: a proposal is admitted only after the operator replied to an answer.
    private static ConversationState Discussed(string userTurn) => State(userTurn) with
    {
        Transcript =
        [
            new TranscriptTurn(TranscriptRole.User, "we need widgets", DateTimeOffset.UtcNow),
            new TranscriptTurn(TranscriptRole.Assistant, "Found the service; two open questions.",
                DateTimeOffset.UtcNow, SpecDialogTurnKind.Answer),
            new TranscriptTurn(TranscriptRole.User, userTurn, DateTimeOffset.UtcNow),
        ],
    };

    private static ProjectMap CannedMap() => new(
        "csharp", ["net8"],
        [new Module("src", ModuleRole.Production, [])],
        [], [], new Conventions(null, null, null),
        new CiConfig(false, null, null, null));

    /// <summary>
    /// The p0315c filing bed: the REAL TicketFilingOutcomeSink over a migrated
    /// SQLite session store, with the recording ticket provider at the tracker
    /// boundary and the recording adapter scripting the approval button.
    /// </summary>
    private sealed class FilingBed : IAsyncDisposable
    {
        public required RealCompositionHarness Harness { get; init; }
        public required InMemoryDialogueBridge Bridge { get; init; }
        public required RecordingChatAdapter Adapter { get; init; }
        public required RecordingTicketProvider Tickets { get; init; }
        public required string DbPath { get; init; }

        public static async Task<FilingBed> BuildAsync(string? autoAnswer)
        {
            var bridge = new InMemoryDialogueBridge();
            var adapter = new RecordingChatAdapter { AutoAnswer = autoAnswer };
            var tickets = new RecordingTicketProvider();
            var dbPath = Path.Combine(
                Path.GetTempPath(), $"agentsmith-harness-{Guid.NewGuid():N}.db");
            var harness = BuildHarness(bridge, adapter, services =>
            {
                // The server assumes a migrated schema (migrations are an
                // explicit deployment step); the bed points the context at a
                // fresh SQLite file and migrates it below.
                services.RemoveAll<DbContextOptions<AgentSmithDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<AgentSmithDbContext>();
                services.AddDbContext<AgentSmithDbContext>(b => b.UseSqlite($"Data Source={dbPath}"));
                // The tracker HTTP boundary for the REAL filing sink.
                services.RemoveAll<ITicketProviderFactory>();
                services.AddSingleton<ITicketProviderFactory>(
                    new RecordingTicketProviderFactory(tickets));
            });
            await using (var scope = harness.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>()
                    .Database.MigrateAsync();
            }
            return new FilingBed
            {
                Harness = harness, Bridge = bridge, Adapter = adapter,
                Tickets = tickets, DbPath = dbPath,
            };
        }

        public async Task<ConversationState> OpenSessionAsync(string threadId)
        {
            await using var scope = Harness.Services.CreateAsyncScope();
            var sessions = scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>();
            return await sessions.OpenAsync(
                "slack", "C-harness", threadId, "U-harness",
                new ActiveScope { Project = Project, Repos = [Repo] },
                CancellationToken.None);
        }

        public async Task<IReadOnlyList<string>> TrailAsync(string threadId)
        {
            await using var scope = Harness.Services.CreateAsyncScope();
            var sessions = scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>();
            var state = await sessions.GetOpenByThreadAsync("slack", threadId, CancellationToken.None);
            return [.. state!.Transcript
                .Where(t => t.Role == TranscriptRole.Assistant)
                .Select(t => t.Text)];
        }

        public async ValueTask DisposeAsync()
        {
            await Harness.DisposeAsync();
            SqliteConnection.ClearAllPools();
            foreach (var file in new[] { DbPath, $"{DbPath}-wal", $"{DbPath}-shm" })
                if (File.Exists(file)) File.Delete(file);
        }
    }

    private sealed class RecordingOutcomeSink : IOutcomeSink
    {
        private readonly List<OutcomeProposal> _accepted = [];
        public IReadOnlyList<OutcomeProposal> Accepted => _accepted;

        public Task AcceptAsync(
            ConversationState state, OutcomeProposal proposal, bool mayStartRuns,
            CancellationToken cancellationToken)
        {
            lock (_accepted) _accepted.Add(proposal);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTicketProvider : ITicketProvider
    {
        private readonly List<(string Title, string Body, IReadOnlyList<string> Labels)> _created = [];
        private readonly List<(TicketId Id, string Comment)> _comments = [];
        private readonly List<(string Child, string Parent)> _links = [];

        public IReadOnlyList<(string Child, string Parent)> Links
        {
            get { lock (_links) return [.. _links]; }
        }

        public IReadOnlyList<(string Title, string Body, IReadOnlyList<string> Labels)> Created
        {
            get { lock (_created) return [.. _created]; }
        }

        public IReadOnlyList<(TicketId Id, string Comment)> Comments
        {
            get { lock (_comments) return [.. _comments]; }
        }

        public string ProviderType => "recording";

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ConnectionProbeResult.Reachable(0));

        public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels, string? kind,
            CancellationToken cancellationToken)
        {
            lock (_created)
            {
                _created.Add((title, description, labels));
                return Task.FromResult(new CreatedTicket(
                    new TicketId(_created.Count.ToString()),
                    $"https://tracker.test/{_created.Count}"));
            }
        }

        public Task<ParentLinkResult> LinkToParentAsync(
            CreatedTicket child, TicketId parent, CancellationToken cancellationToken)
        {
            lock (_links) _links.Add((child.Id.Value, parent.Value));
            return Task.FromResult(ParentLinkResult.Linked);
        }

        public Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken)
        {
            lock (_comments) _comments.Add((ticketId, comment));
            return Task.CompletedTask;
        }

        public Task<TicketFinalizeResult> FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken) =>
            Task.FromResult(TicketFinalizeResult.Moved());
    }

    private sealed class RecordingTicketProviderFactory(RecordingTicketProvider provider)
        : ITicketProviderFactory
    {
        public ITicketProvider Create(TrackerConnection config) => provider;
    }

    /// <summary>
    /// The chat platform boundary: records the approval questions (the blocks
    /// surface) and every threaded info post; AutoAnswer scripts the button
    /// click, otherwise the ask waits until the turn cancels it.
    /// </summary>
    private sealed class RecordingChatAdapter : IPlatformAdapter
    {
        private readonly List<DialogQuestion> _questions = [];
        private readonly List<string> _sent = [];
        private readonly TaskCompletionSource<DialogQuestion> _firstQuestion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? AutoAnswer { get; init; }
        public string Platform => "slack";

        public IReadOnlyList<DialogQuestion> Questions
        {
            get { lock (_questions) return [.. _questions]; }
        }

        public IReadOnlyList<string> SentTexts
        {
            get { lock (_sent) return [.. _sent]; }
        }

        public async Task<DialogQuestion> WaitForQuestionAsync() =>
            await _firstQuestion.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public async Task<DialogAnswer?> AskTypedQuestionAsync(
            string channelId, DialogQuestion question, string? threadId,
            CancellationToken cancellationToken)
        {
            lock (_questions) _questions.Add(question);
            _firstQuestion.TrySetResult(question);
            if (AutoAnswer is not null)
                return new DialogAnswer(
                    question.QuestionId, AutoAnswer, null, DateTimeOffset.UtcNow, "U-harness");
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The text path answered first — the button ask is cancelled.
            }
            return null;
        }

        public Task SendInfoAsync(string channelId, string title, string text,
            string? threadId, CancellationToken cancellationToken)
        {
            lock (_sent) _sent.Add(text);
            return Task.CompletedTask;
        }

        public Task SendMessageAsync(string channelId, string text, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SendProgressAsync(string channelId, int step, int total, string commandName,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendDoneAsync(string channelId, string summary, string? prUrl,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendErrorAsync(string channelId, ErrorContext errorContext,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task UpdateQuestionAnsweredAsync(string channelId, string messageId, string questionText,
            string answer, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendDetailAsync(string channelId, string text, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SendClarificationAsync(string channelId, string suggestion,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>
    /// The in-memory stand-in for the Redis-stream dialogue pair: answers
    /// (button bridge or thread text) complete the matching waiter. The bus
    /// subscription stays silent until cancelled — the pump's thread relay is
    /// production plumbing these tests do not assert.
    /// </summary>
    private sealed class InMemoryDialogueBridge : IDialogueTransport, IMessageBus
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<
            string, TaskCompletionSource<DialogAnswer>> _answers = new();

        public Task PublishQuestionAsync(
            string jobId, DialogQuestion question, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task PublishAnswerAsync(string jobId, DialogAnswer answer, CancellationToken cancellationToken)
        {
            Waiter(jobId, answer.QuestionId).TrySetResult(answer);
            return Task.CompletedTask;
        }

        public async Task<DialogAnswer?> WaitForAnswerAsync(
            string jobId, string questionId, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var waiter = Waiter(jobId, questionId).Task;
            var finished = await Task.WhenAny(waiter, Task.Delay(TimeSpan.FromSeconds(10), cancellationToken));
            return finished == waiter ? await waiter : null;
        }

        private TaskCompletionSource<DialogAnswer> Waiter(string jobId, string questionId) =>
            _answers.GetOrAdd($"{jobId}:{questionId}",
                _ => new TaskCompletionSource<DialogAnswer>(TaskCreationOptions.RunContinuationsAsynchronously));

        public Task PublishAsync(BusMessage message, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task PublishAnswerAsync(
            string jobId, string questionId, string content, CancellationToken cancellationToken) =>
            PublishAnswerAsync(jobId,
                new DialogAnswer(questionId, content, null, DateTimeOffset.UtcNow, "U-harness"),
                cancellationToken);

        public async IAsyncEnumerable<BusMessage> SubscribeToJobAsync(
            string jobId, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The turn/confirmation window ended — the silent stream closes.
            }
            yield break;
        }

        public Task<BusMessage?> ReadAnswerAsync(
            string jobId, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult<BusMessage?>(null);

        public Task CleanupJobAsync(string jobId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class StubSkillsCatalogResolver : ISkillsCatalogResolver
    {
        public Task<CatalogResolution> EnsureResolvedAsync(
            SkillsConfig config, CancellationToken cancellationToken) =>
            Task.FromResult(new CatalogResolution(
                "/tmp/agentsmith-harness/empty-catalog", "harness",
                SkillsSourceMode.Default, "https://stub.test/catalog", FromCache: true));
    }

    private sealed class CannedProjectMapStore(ProjectMap map) : IProjectMapStore
    {
        public Task<ProjectMap?> TryGetAsync(
            string cacheKeyId, string contentHash, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectMap?>(null);

        public Task SetAsync(
            string cacheKeyId, string contentHash, ProjectMap value, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ProjectMap>> ListByPrefixAsync(
            string cacheKeyPrefix, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProjectMap>>([map]);
    }
}
