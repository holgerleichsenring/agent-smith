using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-12-92e9a: a design turn's thread is stored as it grows — through the REAL server composition, a turn that
/// throws after its model calls leaves their thread, written before the turn let go of its gate, and the next turn
/// starts from it.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDialogTurnRunnerTests
{
    private const string Repo = "spec-dialog-fixture";
    private const string Project = "fixture-spec-dialog";
    private const string Dialog = "d-thread-durable";
    private const string Platform = DispatcherDefaults.PlatformDashboard;
    private const string RouterText = "public sealed class Router { public void Dispatch(Intent intent) { } }";

    [Fact]
    public async Task Runner_ThrownTurn_WriterClosedBeforeGateReleased()
    {
        await using var harness = BuildHarness();
        var state = await AppendAsync(harness, "how does the router dispatch?", open: true);
        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Router.cs"}""")
            .EnqueueText("The router hands the intent to the engine.");

        (await TurnAsync(harness, state)).Should().BeNull("the turn threw after its model calls");

        harness.Services.GetRequiredService<SpecDialogTurnGate>().Liveness(state.JobId).Computing.Should().BeFalse();
        var row = await NewestAsync(harness, state.JobId);
        row.Should().NotBeNull("the writer was closed and awaited before the turn returned");
        row!.MessagesJson.Should().Contain("Router").And.Contain("The router hands the intent to the engine.");
        row.ReplyAt.Should().BeNull("no reply was kept");
    }

    [Fact]
    public async Task Runner_TurnAfterAThrownOne_StartsFromTheStoredThread()
    {
        await using var harness = BuildHarness();
        var first = await AppendAsync(harness, "how does the router dispatch?", open: true);
        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Router.cs"}""")
            .EnqueueText("The router hands the intent to the engine.");
        await TurnAsync(harness, first);
        var second = await AppendAsync(harness, "and what happens on a miss?");
        var secondTurnStarts = harness.ChatClient.CallMessages.Count;
        harness.ChatClient.EnqueueText("A miss falls back to the default intent.");

        await TurnAsync(harness, second);

        var contents = harness.ChatClient.CallMessages[secondTurnStarts].SelectMany(m => m.Contents).ToList();
        contents.OfType<FunctionResultContent>().Should().ContainSingle(r => r.Result!.ToString()!.Contains(RouterText));
    }

    private static async Task<ConversationState> AppendAsync(RealCompositionHarness harness, string text, bool open = false)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>();
        if (open)
            await sessions.OpenAsync(Platform, Dialog, Dialog, "U-durable",
                new ActiveScope { Project = Project, Repos = [Repo] }, CancellationToken.None);
        return (await sessions.AppendTurnAsync(Platform, Dialog, TranscriptRole.User, text, null, null, CancellationToken.None))!;
    }

    private static async Task<SpecDialogTurnResult?> TurnAsync(RealCompositionHarness harness, ConversationState state)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>()
            .RunTurnAsync(state, new TurnPoster("U1", false), CancellationToken.None);
    }

    private static async Task<SpecDialogThread?> NewestAsync(RealCompositionHarness harness, string sessionId)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SpecDialogThreadRepository>().NewestAsync(sessionId, default);
    }

    private static RealCompositionHarness BuildHarness()
    {
        var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubCatalogResolver());
            ThrowingOnRunFinished.Install(services);
        });
        harness.StubSandboxFactory!.SourceFiles = new(StringComparer.Ordinal) { ["src/Router.cs"] = RouterText };
        return harness;
    }
}
