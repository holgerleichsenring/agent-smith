using System.Runtime.CompilerServices;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
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
public sealed class SpecDialogOutcomeTests
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
          - phase: p9000b
            goal: "Widget API on top of the storage layer"
            requires: [p9000a]
            steps:
              - id: api
                action: "Add the widget endpoint"
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
        var state = State("add a null check to AppendTurnAsync");

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
        var state = State("draft the widget phase now");

        var result = await RunTurnAsync(harness, state);
        await RunFlowAsync(harness, state, result.Outcome);

        var phase = result.Outcome.Should().BeOfType<PhaseOutcome>().Subject;
        phase.Draft.PhaseId.Should().Be("p9999");
        phase.Draft.Yaml.Should().Contain("goal:", "the full schema-valid spec travels with the proposal");
        sink.Accepted.Should().ContainSingle().Which.Should().BeOfType<PhaseOutcome>();
    }

    [Fact]
    public async Task Outcome_LargeFeature_ProposesEpicWithLinkedRequires()
    {
        var (bridge, adapter, sink) = (new InMemoryDialogueBridge(),
            new RecordingChatAdapter { AutoAnswer = "approve" }, new RecordingOutcomeSink());
        await using var harness = BuildHarness(bridge, adapter, ReplaceSink(sink));
        harness.ChatClient.EnqueueText(EpicOutcomeReply);
        var state = State("build the whole widget platform");

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
        var state = State("draft the widget phase now");

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
    public async Task CreatePhase_PhaseOutcome_FilesOnePhaseTicketWithYamlBlock()
    {
        await using var bed = await FilingBed.BuildAsync(autoAnswer: "approve");
        var state = await bed.OpenSessionAsync("th-phase");
        var draft = new PhaseDraft(
            "p9999", "Add a widget endpoint to the sample service", ValidDraftYaml, []);

        await RunFlowAsync(bed.Harness, state, new PhaseOutcome(draft));

        var created = bed.Tickets.Created.Should().ContainSingle().Subject;
        created.Title.Should().Be("p9999: Add a widget endpoint to the sample service");
        created.Labels.Should().Contain(PhaseTicketRenderer.PhaseLabel);
        var extracted = ExtractYaml(bed.Harness, created.Body);
        extracted.Should().Be(ValidDraftYaml.Trim(),
            "the body's single ```yaml block carries the schema-valid spec verbatim");
        bed.Adapter.SentTexts.Should().Contain(t => t.Contains("https://tracker.test/1"));
    }

    [Fact]
    public async Task CreatePhase_EpicOutcome_FilesLinkedPhaseTicketsWithRequires()
    {
        await using var bed = await FilingBed.BuildAsync(autoAnswer: "approve");
        var state = await bed.OpenSessionAsync("th-epic");
        var epic = new EpicOutcome(
            new PhaseDraft("p9000", "Widget platform end to end",
                "phase: p9000\ngoal: \"Widget platform end to end\"", []),
            [
                new PhaseDraft("p9000a", "Widget storage layer",
                    "phase: p9000a\ngoal: \"Widget storage layer\"\nsteps:\n  - id: store\n    action: \"Add the widget store\"",
                    []),
                new PhaseDraft("p9000b", "Widget API on top of the storage layer",
                    "phase: p9000b\ngoal: \"Widget API on top of the storage layer\"\nrequires: [p9000a]\nsteps:\n  - id: api\n    action: \"Add the widget endpoint\"",
                    ["p9000a"]),
            ]);

        await RunFlowAsync(bed.Harness, state, epic);

        bed.Tickets.Created.Should().HaveCount(3, "parent first, then the slices in order");
        bed.Tickets.Created.Select(t => t.Title).Should().Equal(
            "p9000: Widget platform end to end",
            "p9000a: Widget storage layer",
            "p9000b: Widget API on top of the storage layer");
        bed.Tickets.Created.Should().OnlyContain(t => t.Labels.Contains(PhaseTicketRenderer.PhaseLabel));
        bed.Tickets.Created[0].Body.Should().Contain("## Slices").And.Contain("p9000a").And.Contain("p9000b");
        bed.Tickets.Created[1].Body.Should().Contain("Parent: https://tracker.test/1");
        bed.Tickets.Created[2].Body.Should().Contain("Parent: https://tracker.test/1");
        ExtractYaml(bed.Harness, bed.Tickets.Created[2].Body).Should().Contain("requires: [p9000a]",
            "the child spec carries its requires: edge for the pipeline");
        bed.Tickets.Comments.Should().ContainSingle(
            "the parent links its children — a comment, honestly, since no tracker "
            + "provider exposes native links").Which.Comment.Should()
            .Contain("https://tracker.test/2").And.Contain("https://tracker.test/3");
        bed.Adapter.SentTexts.Should().Contain(t =>
            t.Contains("https://tracker.test/1") && t.Contains("https://tracker.test/2")
            && t.Contains("https://tracker.test/3"));
    }

    [Fact]
    public async Task PhaseTicketRenderer_YamlBlock_SchemaValidAndExtractable()
    {
        await using var bed = await FilingBed.BuildAsync(autoAnswer: null);
        var draft = new PhaseDraft(
            "p9999", "Add a widget endpoint to the sample service", ValidDraftYaml, []);

        var content = new PhaseTicketRenderer().RenderPhase(draft);

        content.Title.Should().Be("p9999: Add a widget endpoint to the sample service");
        content.Body.Should().Contain("## Goal").And.Contain("## Scope",
            "humans read the summary before the machine block");
        var validator = bed.Harness.Services.GetRequiredService<ISpecDraftValidator>();
        var outcome = validator.Validate(content.Body);
        var valid = outcome.Should().BeOfType<SpecDraftValid>(
            "the body holds exactly one ```yaml block and it is schema-valid").Subject;
        valid.Yaml.Should().Be(ValidDraftYaml.Trim(), "the extractor's inverse restores the spec verbatim");
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
        return await flow.HandleAsync(state, outcome, CancellationToken.None);
    }

    // The p0315d inverse: the ticket body must hold exactly ONE ```yaml block
    // and it must be schema-valid — proven by the same validator the draft
    // gate uses.
    private static string ExtractYaml(RealCompositionHarness harness, string body)
    {
        var validator = harness.Services.GetRequiredService<ISpecDraftValidator>();
        return validator.Validate(body).Should().BeOfType<SpecDraftValid>().Subject.Yaml;
    }

    private static ConversationState State(string userTurn) => new()
    {
        JobId = "sess-outcome",
        ChannelId = "C-harness",
        UserId = "U-harness",
        Platform = "slack",
        Project = Project,
        TicketId = 0,
        StartedAt = DateTimeOffset.UtcNow,
        Mode = ConversationMode.SpecDialog,
        ThreadId = "th-outcome",
        Transcript = [new TranscriptTurn(TranscriptRole.User, userTurn, DateTimeOffset.UtcNow)],
        Scope = new ActiveScope { Project = Project, Repos = [Repo] },
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
            ConversationState state, OutcomeProposal proposal, CancellationToken cancellationToken)
        {
            lock (_accepted) _accepted.Add(proposal);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingTicketProvider : ITicketProvider
    {
        private readonly List<(string Title, string Body, IReadOnlyList<string> Labels)> _created = [];
        private readonly List<(TicketId Id, string Comment)> _comments = [];

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
            string title, string description, IReadOnlyList<string> labels,
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

        public Task UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken cancellationToken)
        {
            lock (_comments) _comments.Add((ticketId, comment));
            return Task.CompletedTask;
        }

        public Task FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken) =>
            Task.CompletedTask;
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
