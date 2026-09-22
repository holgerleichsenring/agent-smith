using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.SpecDialog;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-22-2d11b: the second message of a design conversation spawns no container and
/// clones nothing — through the REAL server composition, SpecDialogTurnRunner → the
/// spec-dialog preset → the master → the source scopes, with the LLM scripted and the
/// heartbeat answering for a stub sandbox that has neither an agent nor a Redis.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDialogHoldTests
{
    private const string Repo = "spec-dialog-fixture";
    private const string Project = "fixture-spec-dialog";
    private const string Dialog = "d-hold";

    private readonly RecordingDialogHub _hub = new();

    [Fact]
    public async Task SpecDialogTurn_ASecondTurnOfOneConversation_SpawnsNoContainerAndClonesNothing()
    {
        await using var harness = BuildHarness();
        var state = await OpenAsync(harness);

        await ReadingTurnAsync(harness, state, "Dispatch flows through the intent engine.");
        await ReadingTurnAsync(harness, state, "And the router hands it on.");

        var spawned = harness.StubSandboxFactory!.Spawned;
        spawned.Should().ContainSingle("the second turn reads through the sandbox the first held");
        spawned[0].Spec.ConversationId.Should().Be(state.JobId,
            "a sandbox with no conversation label is reaped inside thirty seconds");
        var steps = spawned[0].Sandbox.RanSteps
            .Where(s => s.Command == "git" && s.Args is not null)
            .Select(s => string.Join(" ", s.Args!))
            .ToList();
        steps.Count(s => s.Contains("clone", StringComparison.Ordinal)).Should().Be(1);
        steps.Should().Contain(s => s.Contains("fetch --depth 1 origin HEAD", StringComparison.Ordinal),
            "the held tree is brought to the remote's own HEAD instead");
        spawned[0].Sandbox.Disposed.Should().BeFalse("the turn releases it rather than tearing it down");
    }

    [Fact]
    public async Task SpecDialogTurn_ASecondTurn_StillPushesOpeningAndReadyToTheDashboard()
    {
        await using var harness = BuildHarness();
        var state = await OpenAsync(harness);

        await ReadingTurnAsync(harness, state, "Dispatch flows through the intent engine.");
        await ReadingTurnAsync(harness, state, "And the router hands it on.");

        Readings().Should().Equal(
            (Repo, "opening"), (Repo, "ready"), (Repo, "opening"), (Repo, "ready"));
    }

    private IReadOnlyList<(string Repo, string State)> Readings() =>
        [.. _hub.Pushes
            .Where(p => p.Method == "SpecDialogReading")
            .Select(p => (SpecDialogReadingPush)p.Args[0]!)
            .Select(p => (p.Repo, p.State))];

    private static async Task ReadingTurnAsync(
        RealCompositionHarness harness, ConversationState state, string answer)
    {
        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Router.cs"}""")
            .EnqueueText(answer);
        await using var scope = harness.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>()
            .RunTurnAsync(state, CancellationToken.None);
    }

    private RealCompositionHarness BuildHarness() =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubCatalogResolver());
            services.RemoveAll<IHubContext<JobsHub>>();
            services.AddSingleton<IHubContext<JobsHub>>(_hub);
            // The stub sandbox runs no agent and the harness Redis is a mock, so the probe
            // that verifies a hold is the one boundary this test must answer for.
            services.RemoveAll<ISandboxHeartbeatProbe>();
            services.AddSingleton<ISandboxHeartbeatProbe>(new AliveSandboxHeartbeat());
        });

    private static async Task<ConversationState> OpenAsync(RealCompositionHarness harness)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var opened = await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>()
            .OpenAsync(DispatcherDefaults.PlatformDashboard, Dialog, Dialog, "U-hold",
                new ActiveScope { Project = Project, Repos = [Repo] }, CancellationToken.None);
        return opened.AppendTurn(new TranscriptTurn(
            TranscriptRole.User, "cut the ordering feature into slices", DateTimeOffset.UtcNow));
    }
}
