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
/// 2026-11-05-862ea: the design master states the work picture through state_work_picture on a conversation with an
/// upload, and the next turn renders the stored picture — through the REAL server composition, SpecDialogTurnRunner →
/// the spec-dialog preset → the master → the tool, with the LLM scripted.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed partial class SpecDialogWorkPictureTests
{
    private const string Repo = "spec-dialog-fixture";
    private const string Project = "fixture-spec-dialog";
    private const string Dialog = "d-work-picture";

    [Fact]
    public async Task SpecDialogHarness_ScriptedMasterStatesWorkPicture_StoredAndRenderedNextTurn()
    {
        await using var harness = BuildHarness();
        var state = await OpenAsync(harness);
        await UploadAsync(harness, state.JobId);
        harness.ChatClient
            .EnqueueToolCall("state_work_picture", """{"kind": "rebuild", "truth": "the uploaded app; the repository provides the host", "size": "series", "effort": "large"}""")
            .EnqueueText("Rebuild, series: the uploaded app is the truth.");

        var first = await TurnAsync(harness, state);
        var firstPrompt = harness.ChatClient.PromptsSeen.Last();
        await StoreAsync(harness, first.WorkPicture!);
        harness.ChatClient.EnqueueText("Still the uploaded app.");
        await TurnAsync(harness, await ReloadAsync(harness, "and the second part?"));

        first.Kind.Should().NotBe(SpecDialogTurnKind.Failure);
        first.WorkPicture.Should().Be(new WorkPicture(
            UndertakingKind.Rebuild, "the uploaded app; the repository provides the host", UndertakingSize.Series,
            AgentSmith.Contracts.Models.Configuration.ComplexityTier.Large));
        firstPrompt.Should().Contain("No work picture is stored yet").And.NotContain("before the repositories");
        harness.ChatClient.PromptsSeen.Last().Should().Contain("## Work picture").And.Contain("- kind: rebuild")
            .And.NotContain("No work picture is stored yet");
    }

    private static async Task<SpecDialogTurnResult> TurnAsync(RealCompositionHarness harness, ConversationState state)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>()
            .RunTurnAsync(state, new TurnPoster("U1", false), CancellationToken.None);
    }

    private static async Task StoreAsync(RealCompositionHarness harness, WorkPicture picture)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SpecDialogWorkPictureStore>()
            .StoreAsync(DispatcherDefaults.PlatformDashboard, Dialog, picture, CancellationToken.None);
    }

    private static async Task<ConversationState> ReloadAsync(RealCompositionHarness harness, string message)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>()
            .GetOpenByThreadAsync(DispatcherDefaults.PlatformDashboard, Dialog, CancellationToken.None);
        return stored!.AppendTurn(new TranscriptTurn(TranscriptRole.User, message, DateTimeOffset.UtcNow));
    }

    private static async Task UploadAsync(RealCompositionHarness harness, string conversationId)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ReferenceSetRepository>().AddAsync(conversationId,
            [new ReferenceFile { RelativePath = "app/a.cs", MediaType = "application/octet-stream", Content = "class A { }"u8.ToArray() }],
            default);
    }

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
            .OpenAsync(DispatcherDefaults.PlatformDashboard, Dialog, Dialog, "U-picture",
                new ActiveScope { Project = Project, Repos = [Repo] }, CancellationToken.None);
        return opened.AppendTurn(new TranscriptTurn(
            TranscriptRole.User, "rebuild the uploaded app", DateTimeOffset.UtcNow));
    }
}
