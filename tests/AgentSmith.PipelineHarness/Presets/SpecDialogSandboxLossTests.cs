using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Sandbox.Wire;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.SpecDialog;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-09-9631a: a sandbox taken away under a design turn, through the REAL server composition.
/// The removal itself is right: it frees capacity. The turn must not stand still because of it.
/// Only the heartbeat and the sandbox's silence are stood in for: a removed stub answers nothing,
/// as a pushed step at a deleted pod does.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDialogSandboxLossTests
{
    private const string Repo = "spec-dialog-fixture";
    private const string Project = "fixture-spec-dialog";
    private const string Dialog = "d-loss";

    private readonly StubLiveness _liveness = new();

    [Fact]
    public async Task SpecDialogTurn_HeldSandboxDisappearsMidTurn_NextReadRecoversAndTheTurnAnswers()
    {
        await using var harness = BuildHarness();
        var state = await OpenAsync(harness);
        harness.ChatClient.EnqueueToolCall("read_file", ReadArgs).EnqueueText("First answer.");
        await TurnAsync(harness, state, CancellationToken.None);
        var held = harness.StubSandboxFactory!.Spawned.Single().Sandbox;
        VanishAfterItsNextRead(held);

        var result = await SecondReadTurnAsync(harness, state);

        result.Reply.Should().Be("Recovered answer.");
        var spawned = harness.StubSandboxFactory.Spawned;
        spawned.Should().HaveCount(2, "the removed sandbox is replaced once");
        held.ForceRemoved.Should().BeTrue();
        spawned[1].Sandbox.RanSteps.Should().Contain(s => s.Kind == StepKind.ReadFile);
        spawned[1].Sandbox.ForceRemoved.Should().BeFalse("the fresh sandbox is what the conversation now holds");
    }

    // Found live: a cancel put a live sandbox back in the hold with the cancelled run's label, the
    // next turn took it because its heartbeat lived, the window ran out during that turn and the
    // reaper deleted it. The retry starts at once, and the loss costs one reopen.
    [Fact]
    public async Task SpecDialogTurn_CancelledThenRetried_NeedsNoWaitAndSurvivesTheReaper()
    {
        await using var harness = BuildHarness();
        var state = await OpenAsync(harness);
        using var cancel = new CancellationTokenSource();
        harness.ChatClient
            .EnqueueToolCall("read_file", ReadArgs)
            .EnqueueDeferred(() =>
            {
                cancel.Cancel();
                cancel.Token.ThrowIfCancellationRequested();
                throw new InvalidOperationException("unreachable");
            });
        await TurnAsync(harness, state, cancel.Token).ContinueWith(_ => { }, TaskScheduler.Default);
        var cancelled = harness.StubSandboxFactory!.Spawned.Single();
        cancelled.Sandbox.Disposed.Should().BeFalse("a cancel keeps the sandbox for the retry");
        var cancelledRun = cancelled.Spec.RunId;
        VanishAfterItsNextRead(cancelled.Sandbox);

        var result = await SecondReadTurnAsync(harness, state);

        result.Reply.Should().Be("Recovered answer.");
        var spawned = harness.StubSandboxFactory.Spawned;
        spawned.Should().HaveCount(2, "the retry re-took the live sandbox, and replaced it once when it went");
        spawned[1].Spec.RunId.Should().NotBe(cancelledRun, "the fresh sandbox carries the live run's label");
    }

    private const string ReadArgs = $$"""{"path": "{{Repo}}/src/Router.cs"}""";

    private void VanishAfterItsNextRead(StubSandbox sandbox)
    {
        sandbox.OnStep = step =>
        {
            if (step.Kind != StepKind.ReadFile) return;
            sandbox.Vanished = true;
            _liveness.Gone.Add(sandbox.JobId);
        };
    }

    private static async Task<SpecDialogTurnResult> SecondReadTurnAsync(
        RealCompositionHarness harness, ConversationState state)
    {
        harness.ChatClient
            .EnqueueToolCall("read_file", ReadArgs)
            .EnqueueToolCall("read_file", ReadArgs)
            .EnqueueText("Recovered answer.");
        // A removed stub never answers and has no step timeout, so finishing at all is the proof.
        return await TurnAsync(harness, state, CancellationToken.None).OrHang("the turn recovering from the loss");
    }

    private static async Task<SpecDialogTurnResult> TurnAsync(
        RealCompositionHarness harness, ConversationState state, CancellationToken ct)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>()
            .RunTurnAsync(state, new TurnPoster("U1", false), ct);
    }

    private RealCompositionHarness BuildHarness() =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubCatalogResolver());
            services.RemoveAll<ISandboxHeartbeatProbe>();
            services.AddSingleton<ISandboxHeartbeatProbe>(_liveness);
            services.RemoveAll<SandboxStepWatch>();
            services.AddSingleton(new SandboxStepWatch(
                _liveness, NullLogger<SandboxStepWatch>.Instance, TimeSpan.FromMilliseconds(20)));
        });

    private static async Task<ConversationState> OpenAsync(RealCompositionHarness harness)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var opened = await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>()
            .OpenAsync(DispatcherDefaults.PlatformDashboard, Dialog, Dialog, "U-loss",
                new ActiveScope { Project = Project, Repos = [Repo] }, CancellationToken.None);
        return opened.AppendTurn(new TranscriptTurn(
            TranscriptRole.User, "cut the ordering feature into slices", DateTimeOffset.UtcNow));
    }

    private sealed class StubLiveness : ISandboxHeartbeatProbe
    {
        public HashSet<string> Gone { get; } = [];

        public Task<bool> IsAliveAsync(string jobId, CancellationToken cancellationToken) =>
            Task.FromResult(!Gone.Contains(jobId));
    }
}
