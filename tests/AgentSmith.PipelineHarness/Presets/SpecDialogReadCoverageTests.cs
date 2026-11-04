using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-04-96a0c: what read_file opened of an upload is kept across turns — through the REAL server composition,
/// SpecDialogTurnRunner → the spec-dialog preset → the master → the upload's reference sandbox, with the LLM scripted.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDialogReadCoverageTests
{
    private const string Repo = "spec-dialog-fixture";
    private const string Project = "fixture-spec-dialog";
    private const string Dialog = "d-coverage";

    [Fact]
    public async Task SpecDialogHarness_ReadsAcrossTwoTurns_SecondPromptSaysTwoOfThree()
    {
        await using var harness = BuildHarness();
        var state = await OpenAsync(harness);
        var setId = await UploadAsync(harness, state.JobId);
        harness.ChatClient
            .EnqueueToolCall("read_file", """{"path": "reference:app/app/a.cs"}""")
            .EnqueueToolCall("read_file", """{"path": "reference:app/app/b.cs"}""")
            .EnqueueText("The app has two classes I read.");

        var first = await TurnAsync(harness, state);
        harness.ChatClient.EnqueueText("Still the same app.");
        await TurnAsync(harness, state);

        first.Outcome.Uploads.Should().ContainSingle().Which.Should().Be(
            new UploadProvenance("reference:app", setId, ThisTurn: 2, Opened: 2, Files: 3));
        harness.ChatClient.PromptsSeen.Last(p => p.Contains("## Material the operator uploaded", StringComparison.Ordinal))
            .Should().Contain("Earlier turns of this conversation opened 2 of the 3 files read_file can read")
            .And.Contain("`app/c.cs`");
    }

    [Fact]
    public async Task SpecDialogTurnRunner_AFailedTurn_StoresNoReads()
    {
        await using var harness = BuildHarness();
        var state = await OpenAsync(harness);
        await UploadAsync(harness, state.JobId);
        harness.ChatClient
            .EnqueueToolCall("read_file", """{"path": "reference:app/app/a.cs"}""")
            .EnqueueThrow(new InvalidOperationException("the model went away"));

        var result = await TurnAsync(harness, state);

        result.Kind.Should().Be(SpecDialogTurnKind.Failure);
        await using var scope = harness.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<ReferenceReadRepository>().ReadsAsync(state.JobId, default))
            .Should().BeEmpty("a failed turn's reading never reached a kept answer");
    }

    private static async Task<SpecDialogTurnResult> TurnAsync(RealCompositionHarness harness, ConversationState state)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>()
            .RunTurnAsync(state, new TurnPoster("U1", false), CancellationToken.None);
    }

    private static async Task<string> UploadAsync(RealCompositionHarness harness, string conversationId)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var set = await scope.ServiceProvider.GetRequiredService<ReferenceSetRepository>().AddAsync(conversationId,
            [File("app/a.cs"), File("app/b.cs"), File("app/c.cs")], default);
        return set.SetId;
    }

    private static ReferenceFile File(string path) =>
        new() { RelativePath = path, MediaType = "application/octet-stream", Content = "class A { }"u8.ToArray() };

    private static RealCompositionHarness BuildHarness() =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubCatalogResolver());
            services.RemoveAll<ISandboxHeartbeatProbe>();
            services.AddSingleton<ISandboxHeartbeatProbe>(new AliveSandboxHeartbeat());
        });

    private static async Task<ConversationState> OpenAsync(RealCompositionHarness harness)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var opened = await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>()
            .OpenAsync(DispatcherDefaults.PlatformDashboard, Dialog, Dialog, "U-coverage",
                new ActiveScope { Project = Project, Repos = [Repo] }, CancellationToken.None);
        return opened.AppendTurn(new TranscriptTurn(
            TranscriptRole.User, "what does the uploaded app do?", DateTimeOffset.UtcNow));
    }
}
