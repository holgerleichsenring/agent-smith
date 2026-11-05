using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
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
/// 2026-11-04-96a0a: the design master names an unnamed conversation through name_conversation, and the turn result
/// carries that name — through the REAL server composition, SpecDialogTurnRunner → the spec-dialog preset → the
/// master → the tool, with the LLM scripted.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDialogSubjectTests
{
    private const string Repo = "spec-dialog-fixture";
    private const string Project = "fixture-spec-dialog";
    private const string Dialog = "d-subject";
    private const string Name = "Wie der Router Absichten weitergibt";

    [Fact]
    public async Task SpecDialogHarness_ScriptedMasterNamesTheConversation_TurnResultCarriesTheSubject()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubCatalogResolver());
        });
        var state = await OpenAsync(harness);
        harness.ChatClient
            .EnqueueToolCall("name_conversation", $$"""{"subject": "{{Name}}"}""")
            .EnqueueText("Der Router reicht die Absicht an die Engine weiter.");

        await using var scope = harness.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>()
            .RunTurnAsync(state, new TurnPoster("U1", false), CancellationToken.None);

        result.Kind.Should().NotBe(SpecDialogTurnKind.Failure);
        result.Subject.Should().Be(Name);
    }

    private static async Task<ConversationState> OpenAsync(RealCompositionHarness harness)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        var opened = await scope.ServiceProvider.GetRequiredService<SpecDialogSessionManager>()
            .OpenAsync(DispatcherDefaults.PlatformDashboard, Dialog, Dialog, "U-subject",
                new ActiveScope { Project = Project, Repos = [Repo] }, CancellationToken.None);
        return opened.AppendTurn(new TranscriptTurn(
            TranscriptRole.User, "wie gibt der Router Absichten weiter?", DateTimeOffset.UtcNow));
    }
}
