using System.Runtime.CompilerServices;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0315e: typed outcome resolution end-to-end through the REAL server
/// composition — SpecDialogTurnRunner resolves the master's terminal output
/// into an OutcomeProposal, and SpecDialogOutcomeFlow confirms it in-thread
/// (DialogQuestion(Approval) over the dialogue transport) before anything
/// reaches the outcome sink. Overridden boundaries, each named: the dialogue
/// transport + message bus (production = Redis streams; here one in-memory
/// bridge) and IOutcomeSink (the exact seam p0315c replaces — a recording
/// sink observes what routes; the default sink's persistence is unit-tested
/// over real SQLite in SpecDialogOutcomeStoreTests).
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDialogOutcomeTests
{
    private const string Project = "fixture-spec-dialog";
    private const string Repo = "spec-dialog-fixture";

    private const string ValidDraft =
        """
        ```yaml
        phase: p9999
        goal: "Add a widget endpoint to the sample service"
        steps:
          - id: impl
            action: "Add the widget endpoint + handler"
        tests:
          - "Widget_Get_ReturnsWidget"
        done:
          - "GET /widget returns the widget"
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

    [Fact]
    public async Task Outcome_ExplainQuestion_ResolvesAnswer_NoArtifact()
    {
        var (bridge, sink) = (new InMemoryDialogueBridge(), new RecordingOutcomeSink());
        await using var harness = BuildHarness(bridge, sink);
        const string answer = "Dispatch flows through the intent engine into the per-intent handlers.";
        harness.ChatClient.EnqueueText(answer);
        var state = State("how does message dispatch work?");

        var result = await RunTurnAsync(harness, state);
        await RunFlowAsync(harness, state, result.Outcome);

        result.Outcome.Should().BeOfType<AnswerOutcome>();
        result.Reply.Should().Be(answer).And.NotContain("```", "an answer carries no artifact");
        bridge.Questions.Should().BeEmpty("an answer needs no confirmation");
        sink.Accepted.Should().BeEmpty("an answer routes nowhere");
    }

    [Fact]
    public async Task Outcome_SmallChange_ResolvesBug_RoutesFixBug()
    {
        var (bridge, sink) = (new InMemoryDialogueBridge { AutoAnswer = "approve" }, new RecordingOutcomeSink());
        await using var harness = BuildHarness(bridge, sink);
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
        var (bridge, sink) = (new InMemoryDialogueBridge { AutoAnswer = "approve" }, new RecordingOutcomeSink());
        await using var harness = BuildHarness(bridge, sink);
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
        var (bridge, sink) = (new InMemoryDialogueBridge { AutoAnswer = "approve" }, new RecordingOutcomeSink());
        await using var harness = BuildHarness(bridge, sink);
        harness.ChatClient.EnqueueText(EpicOutcomeReply);
        var state = State("build the whole widget platform");

        var result = await RunTurnAsync(harness, state);
        await RunFlowAsync(harness, state, result.Outcome);

        var epic = result.Outcome.Should().BeOfType<EpicOutcome>().Subject;
        epic.Parent.PhaseId.Should().Be("p9000");
        epic.Children.Select(c => c.PhaseId).Should().Equal("p9000a", "p9000b");
        epic.Children[1].Requires.Should().ContainSingle("the slices are linked by requires: edges")
            .Which.Should().Be("p9000a");
        bridge.Questions.Should().ContainSingle().Which.Text.Should()
            .Contain("p9000a").And.Contain("requires: p9000a",
                "the epic slice shape is shown in-thread before anything is filed");
        sink.Accepted.Should().ContainSingle().Which.Should().BeOfType<EpicOutcome>();
    }

    [Fact]
    public async Task Outcome_Proposed_ConfirmedBeforeFiling()
    {
        var (bridge, sink) = (new InMemoryDialogueBridge(), new RecordingOutcomeSink());
        await using var harness = BuildHarness(bridge, sink);
        harness.ChatClient.EnqueueText($"Draft:\n{ValidDraft}");
        var state = State("draft the widget phase now");

        var result = await RunTurnAsync(harness, state);
        var flow = RunFlowAsync(harness, state, result.Outcome);
        var question = await bridge.WaitForQuestionAsync();

        sink.Accepted.Should().BeEmpty("nothing routes before the explicit in-thread confirmation");
        question.Type.Should().Be(QuestionType.Approval);
        question.Text.Should().Contain("p9999", "the proposed outcome is shown for confirmation");

        await bridge.PublishAnswerAsync(state.JobId,
            new DialogAnswer(question.QuestionId, "approve", null, DateTimeOffset.UtcNow, "U-harness"),
            CancellationToken.None);
        await flow;

        sink.Accepted.Should().ContainSingle("the approval releases the proposal to the sink");
    }

    // ---- harness plumbing ----

    private static RealCompositionHarness BuildHarness(
        InMemoryDialogueBridge bridge, RecordingOutcomeSink sink) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            // Same three boundary overrides as the p0315b SpecDialogTests bed
            // (skills catalog = network, project map = Redis, prompt catalog =
            // skill data).
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubSkillsCatalogResolver());
            services.RemoveAll<IProjectMapStore>();
            services.AddSingleton<IProjectMapStore>(new CannedProjectMapStore(CannedMap()));
            // Dialogue transport + message bus are Redis streams in production;
            // the bridge is their in-memory pair so the confirmation Q&A works.
            services.RemoveAll<IDialogueTransport>();
            services.AddSingleton<IDialogueTransport>(bridge);
            services.RemoveAll<IMessageBus>();
            services.AddSingleton<IMessageBus>(bridge);
            // The seam under test: p0315c replaces this sink with real filing.
            services.RemoveAll<IOutcomeSink>();
            services.AddSingleton<IOutcomeSink>(sink);
        });

    private static async Task<SpecDialogTurnResult> RunTurnAsync(
        RealCompositionHarness harness, ConversationState state)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>();
        return await runner.RunTurnAsync(state, CancellationToken.None);
    }

    private static async Task RunFlowAsync(
        RealCompositionHarness harness, ConversationState state, OutcomeProposal outcome)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var flow = scope.ServiceProvider.GetRequiredService<SpecDialogOutcomeFlow>();
        await flow.HandleAsync(state, outcome, CancellationToken.None);
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

    /// <summary>
    /// The in-memory stand-in for the Redis-stream dialogue pair: questions
    /// published on the transport are recorded (and optionally auto-answered),
    /// answers complete the matching waiter. The bus subscription stays silent
    /// until cancelled — the pump's thread relay is production plumbing these
    /// tests do not assert.
    /// </summary>
    private sealed class InMemoryDialogueBridge : IDialogueTransport, IMessageBus
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<
            string, TaskCompletionSource<DialogAnswer>> _answers = new();
        private readonly List<DialogQuestion> _questions = [];
        private readonly TaskCompletionSource<DialogQuestion> _firstQuestion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? AutoAnswer { get; init; }

        public IReadOnlyList<DialogQuestion> Questions
        {
            get { lock (_questions) return [.. _questions]; }
        }

        public async Task<DialogQuestion> WaitForQuestionAsync() =>
            await _firstQuestion.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public async Task PublishQuestionAsync(
            string jobId, DialogQuestion question, CancellationToken cancellationToken)
        {
            lock (_questions) _questions.Add(question);
            _firstQuestion.TrySetResult(question);
            if (AutoAnswer is not null)
                await PublishAnswerAsync(jobId,
                    new DialogAnswer(question.QuestionId, AutoAnswer, null, DateTimeOffset.UtcNow, "U-harness"),
                    cancellationToken);
        }

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
