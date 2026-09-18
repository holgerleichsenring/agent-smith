using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Turns;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.SpecDialog;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-18-2f8b: while a design turn computes, the gate says so — through the REAL server
/// composition, with the LLM scripted, so the span the page is shown is the span the turn
/// actually spent working rather than one a test asserted about a stub.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDialogTurnLivenessTests
{
    private const string Repo = "spec-dialog-fixture";
    private const string Project = "fixture-spec-dialog";
    private const string Dialog = "d-liveness";

    private readonly RecordingDialogHub _hub = new();

    [Fact]
    public async Task TurnRunner_WhileComputing_TheGateSaysSo()
    {
        await using var harness = BuildHarness();
        var gate = harness.Services.GetRequiredService<SpecDialogTurnGate>();
        var state = await OpenAsync(harness, DispatcherDefaults.PlatformDashboard, Dialog);
        SpecDialogTurnLivenessView? during = null;
        harness.ChatClient.EnqueueDeferred(() =>
        {
            during = gate.Liveness(state.JobId);
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "Here is the slice."));
        });

        await RunTurnAsync(harness, state);

        during.Should().NotBeNull("the turn was mid-computation when the model was asked");
        during!.Computing.Should().BeTrue();
        gate.Liveness(state.JobId).Computing.Should().BeFalse(
            "the answer is the outcome; a finished turn is not still thinking");
    }

    /// <summary>
    /// The span is closed in a finally, so a turn that ended by throwing leaves no session
    /// marked as computing forever — which would show every later reader a working line that
    /// never stops.
    /// </summary>
    [Fact]
    public async Task TurnRunner_TurnThatThrew_IsNoLongerComputing()
    {
        // A fault in the run's prologue is the one that still escapes the pipeline: it fires
        // before the executor's own guard and is rethrown (ExecutePipelineUseCase.cs:70-74).
        await using var harness = BuildHarness(new BrokenCatalogResolver());
        var gate = harness.Services.GetRequiredService<SpecDialogTurnGate>();
        var state = await OpenAsync(harness, DispatcherDefaults.PlatformDashboard, Dialog);

        var run = async () => await RunTurnAsync(harness, state);

        await run.Should().ThrowAsync<InvalidOperationException>();
        gate.Liveness(state.JobId).Computing.Should().BeFalse();
    }

    /// <summary>The skills catalog cannot be materialised — a fault in the run's prologue.</summary>
    private sealed class BrokenCatalogResolver : ISkillsCatalogResolver
    {
        public Task<CatalogResolution> EnsureResolvedAsync(
            SkillsConfig config, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("the skills catalog could not be materialised");
    }

    /// <summary>
    /// The router holds the TURN across the approval wait that follows the computation, and
    /// that wait is a wait on a person. The two spans are not the same: the gate is still
    /// entered while nothing is being computed. The wait is entered for real here — the
    /// confirmation gate is resolved and its transport is what reads the liveness.
    /// </summary>
    [Fact]
    public async Task TurnRunner_AcrossTheApprovalWait_IsNotComputing()
    {
        var waiting = new AnsweringTransport();
        await using var harness = BuildHarness(transport: waiting);
        var gate = harness.Services.GetRequiredService<SpecDialogTurnGate>();
        var state = await OpenAsync(harness, DispatcherDefaults.PlatformDashboard, Dialog);
        gate.TryEnter(state.JobId).Should().BeTrue("the router enters before the turn runs");
        harness.ChatClient.EnqueueText("Here is the slice.");
        await RunTurnAsync(harness, state);

        await ConfirmAsync(harness, state, waiting);

        waiting.WhileWaiting.Should().NotBeNull("the approval gate did wait");
        waiting.WhileWaiting!.Computing.Should().BeFalse(
            "waiting for an approval is waiting on a person, not computing");
        gate.TryEnter(state.JobId).Should().BeFalse(
            "the turn stays entered across that wait, which is why the two spans differ");
    }

    /// <summary>The computing span must be closed by something that always runs. A throw
    /// while the activity scope is being opened left it open for the life of the process, and
    /// every later reader of that conversation saw a working line that never stopped.</summary>
    [Fact]
    public async Task TurnRunner_ObserverThatCouldNotBeSet_IsNoLongerComputing()
    {
        await using var harness = BuildHarness(observers: new BrokenObserverAccessor());
        var gate = harness.Services.GetRequiredService<SpecDialogTurnGate>();
        var state = await OpenAsync(harness, DispatcherDefaults.PlatformDashboard, Dialog);

        var run = async () => await RunTurnAsync(harness, state);

        await run.Should().ThrowAsync<InvalidOperationException>();
        gate.Liveness(state.JobId).Computing.Should().BeFalse();
    }

    /// <summary>
    /// A sequence alone repeats every turn. The page's only other defence was the reply push
    /// that clears its list, and a reconnect loses pushes — so the next turn's steps would be
    /// filtered out as duplicates of the dead turn's. Each step names the turn it belongs to.
    /// </summary>
    [Fact]
    public async Task TurnRunner_StepsOfTwoTurns_CarryDifferentTurnIdentities()
    {
        await using var harness = BuildHarness();
        var state = await OpenAsync(harness, DispatcherDefaults.PlatformDashboard, Dialog);
        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Router.cs"}""")
            .EnqueueText("Dispatch flows through the intent engine.");
        await RunTurnAsync(harness, state);
        var first = Turns();

        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Router.cs"}""")
            .EnqueueText("And nothing else touches it.");
        await RunTurnAsync(harness, state);

        var all = Turns();
        first.Should().ContainSingle();
        all.Skip(first.Count).Should().ContainSingle()
            .Which.Should().NotBe(first[0],
                "the two steps are both seq 1; only the turn they name tells them apart");
    }

    private IReadOnlyList<DateTimeOffset> Turns() =>
        [.. _hub.Pushes
            .Where(p => p.Method == "SpecDialogActivity")
            .Select(p => ((SpecDialogActivityPush)p.Args[0]!).TurnStartedAt)];

    private static async Task ConfirmAsync(
        RealCompositionHarness harness, ConversationState state, AnsweringTransport waiting)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        waiting.Gate = harness.Services.GetRequiredService<SpecDialogTurnGate>();
        waiting.SessionId = state.JobId;
        await scope.ServiceProvider.GetRequiredService<SpecDialogOutcomeConfirmer>()
            .ConfirmAsync(
                state, new PhaseOutcome(new PhaseDraft("p9001", "a goal", "phase: p9001", [])),
                CancellationToken.None);
    }

    /// <summary>Answers the approval the moment it is asked, reading the liveness first —
    /// which is the only moment the turn is entered and not computing.</summary>
    private sealed class AnsweringTransport : IDialogueTransport
    {
        public SpecDialogTurnGate? Gate { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public SpecDialogTurnLivenessView? WhileWaiting { get; private set; }

        public Task PublishQuestionAsync(
            string jobId, DialogQuestion question, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<DialogAnswer?> WaitForAnswerAsync(
            string jobId, string questionId, TimeSpan timeout, CancellationToken cancellationToken)
        {
            WhileWaiting = Gate?.Liveness(SessionId);
            return Task.FromResult<DialogAnswer?>(
                new DialogAnswer(questionId, "no", null, DateTimeOffset.UtcNow, "U-liveness"));
        }

        public Task PublishAnswerAsync(
            string jobId, DialogAnswer answer, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    /// <summary>An accessor that cannot take an observer — the scope the runner opens throws.</summary>
    private sealed class BrokenObserverAccessor : ITurnActivityObserverAccessor
    {
        public ITurnActivityObserver? Current => null;

        public IDisposable Observe(ITurnActivityObserver observer) =>
            throw new InvalidOperationException("the activity scope could not be opened");
    }

    [Fact]
    public async Task TurnRunner_Steps_CarryASequenceThatRestartsEachTurn()
    {
        await using var harness = BuildHarness();
        var state = await OpenAsync(harness, DispatcherDefaults.PlatformDashboard, Dialog);
        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Router.cs"}""")
            .EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Router.cs"}""")
            .EnqueueText("Dispatch flows through the intent engine.");
        await RunTurnAsync(harness, state);
        var first = Sequences();

        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Router.cs"}""")
            .EnqueueText("And nothing else touches it.");
        await RunTurnAsync(harness, state);

        first.Should().Equal([1, 2],
            "two identical calls are two steps: a page merging by value would show one");
        Sequences().Skip(first.Count).Should().Equal([1],
            "each turn numbers its own steps, so the page never merges one turn's into another's");
    }

    private IReadOnlyList<int> Sequences() =>
        [.. _hub.Pushes
            .Where(p => p.Method == "SpecDialogActivity")
            .Select(p => ((SpecDialogActivityPush)p.Args[0]!).Seq)];

    private RealCompositionHarness BuildHarness(
        ISkillsCatalogResolver? catalog = null,
        IDialogueTransport? transport = null,
        ITurnActivityObserverAccessor? observers = null) =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton(catalog ?? new StubCatalogResolver());
            services.RemoveAll<IHubContext<JobsHub>>();
            services.AddSingleton<IHubContext<JobsHub>>(_hub);
            if (transport is not null)
            {
                services.RemoveAll<IDialogueTransport>();
                services.AddSingleton(transport);
            }
            if (observers is null) return;
            services.RemoveAll<ITurnActivityObserverAccessor>();
            services.AddSingleton(observers);
        });

    private static async Task<ConversationState> OpenAsync(
        RealCompositionHarness harness, string platform, string thread)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var opened = await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>()
            .OpenAsync(platform, thread, thread, "U-liveness",
                new ActiveScope { Project = Project, Repos = [Repo] }, CancellationToken.None);
        return opened.AppendTurn(new TranscriptTurn(
            TranscriptRole.User, "cut the ordering feature into slices", DateTimeOffset.UtcNow));
    }

    private static async Task RunTurnAsync(
        RealCompositionHarness harness, ConversationState state, CancellationToken ct = default)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>()
            .RunTurnAsync(state, ct);
    }
}
