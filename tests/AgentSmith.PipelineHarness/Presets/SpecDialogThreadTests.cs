using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
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
/// A design turn starts from the thread its previous turn ended with — through the REAL server composition,
/// SpecDialogTurnRunner → the spec-dialog preset → the master, kept by SpecDialogTurnKeeper into the database and read
/// back from a fresh scope, with the LLM scripted.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDialogThreadTests
{
    private const string Repo = "spec-dialog-fixture";
    private const string Project = "fixture-spec-dialog";
    private const string Dialog = "d-thread";
    private const string Platform = DispatcherDefaults.PlatformDashboard;
    private const string RouterText = "public sealed class Router { public void Dispatch(Intent intent) { } }";

    [Fact]
    public async Task SpecDialogHarness_SecondTurn_FirstCallCarriesTheFirstTurnsToolResult()
    {
        await using var harness = BuildHarness();
        harness.StubSandboxFactory!.SourceFiles = new(StringComparer.Ordinal) { ["src/Router.cs"] = RouterText };
        var first = await AppendAsync(harness, "how does the router dispatch?", open: true);
        harness.ChatClient
            .EnqueueToolCall("read_file", $$"""{"path": "{{Repo}}/src/Router.cs"}""")
            .EnqueueText("The router hands the intent to the engine.");
        await KeepAsync(harness, first, await TurnAsync(harness, first));
        var second = await AppendAsync(harness, "and what happens on a miss?");
        var secondTurnStarts = harness.ChatClient.CallMessages.Count;
        harness.ChatClient.EnqueueText("A miss falls back to the default intent.");

        await TurnAsync(harness, second);

        var call = harness.ChatClient.CallMessages[secondTurnStarts];
        var contents = call.SelectMany(m => m.Contents).ToList();
        contents.OfType<FunctionCallContent>().Should().ContainSingle(c => c.Name == "read_file");
        contents.OfType<FunctionResultContent>().Should().ContainSingle(r => r.Result!.ToString()!.Contains(RouterText));
        var prompt = call[^1].Text;
        prompt.Should().Contain("[Your earlier turns").And.Contain("[user] and what happens on a miss?")
            .And.NotContain("how does the router dispatch?").And.NotContain("[assistant] The router hands");
    }

    private static async Task<ConversationState> AppendAsync(RealCompositionHarness harness, string text, bool open = false)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>();
        if (open)
            await sessions.OpenAsync(Platform, Dialog, Dialog, "U-thread",
                new ActiveScope { Project = Project, Repos = [Repo] }, CancellationToken.None);
        return (await sessions.AppendTurnAsync(Platform, Dialog, TranscriptRole.User, text, null, null, CancellationToken.None))!;
    }

    private static async Task<SpecDialogTurnResult> TurnAsync(RealCompositionHarness harness, ConversationState state)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>()
            .RunTurnAsync(state, new TurnPoster("U1", false), CancellationToken.None);
    }

    private static async Task KeepAsync(RealCompositionHarness harness, ConversationState state, SpecDialogTurnResult result)
    {
        result.Kind.Should().NotBe(SpecDialogTurnKind.Failure);
        await using var scope = harness.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SpecDialogTurnKeeper>()
            .KeepAsync(state, result, Platform, Dialog, CancellationToken.None);
    }

    private static RealCompositionHarness BuildHarness() =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubCatalogResolver());
        });
}
