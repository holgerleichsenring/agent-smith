using AgentSmith.Application.Prompts;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.PipelineHarness.Evals;
using AgentSmith.Contracts.Services;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Tests.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-09-0620i: a reference turn — the first design turn on an upload and a repository nobody has mapped — through
/// the real server composition, asserting what the turn is told: what each source shows, the repository's slot where
/// the code map stands, no reading order and no proposal ban, and the parts of the prompt in their old places.
/// </summary>
public sealed partial class SpecDialogWorkPictureTests
{
    [Fact]
    public async Task ReferenceTurn_FirstDesignTurnOnUploadAndUnmappedRepo_IsToldItsSituation()
    {
        await using var harness = BuildRealCatalogHarness();
        var state = await OpenAsync(harness);
        await UploadAsync(harness, state.JobId);
        harness.ChatClient.EnqueueText("Rebuild, series.");

        await TurnAsync(harness, state);

        var messages = harness.ChatClient.CallMessages[0];
        var system = string.Join("\n", messages.Where(m => m.Role.Value == "system").Select(m => m.Text));
        var user = string.Join("\n", messages.Where(m => m.Role.Value == "user").Select(m => m.Text));
        user.Should().NotContain("before the repositories").And.NotContain("MAY NOT propose")
            .And.NotContain("Ready to propose").And.NotContain("scaffolding")
            .And.NotContain("The uploads show", "the skill says what each source shows; the turn does not repeat it");
        system.Should().Contain("An upload (a `reference:<name>` address) says what is wanted",
            "the skill's own reading section reaches the model");
        // The fixture cannot be listed remotely, so its slot holds the fallback line; the listed layout itself is pinned
        // by RepositoryLayoutTests. What holds here is that the slot exists for the unmapped repository.
        system.Should().Contain("### Repository: spec-dialog-fixture");

        string[] order =
        [
            "## Design conversation", "rebuild the uploaded app", "## Your reply", "No work picture is stored yet",
        ];
        order.Select(marker => user.IndexOf(marker, StringComparison.Ordinal)).Should()
            .OnlyContain(p => p >= 0).And.BeInAscendingOrder("a changed section stays where it was");
    }

    // The real embedded catalog and prompt catalog, so the design master's system prompt renders its code-map slot.
    private static RealCompositionHarness BuildRealCatalogHarness() =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISkillsCatalogPath>();
            services.AddSingleton<ISkillsCatalogPath, EmbeddedSkillsCatalogPath>();
            services.RemoveAll<IPromptCatalog>();
            services.AddPromptCatalog();
            services.RemoveAll<ISandboxHeartbeatProbe>();
            services.AddSingleton<ISandboxHeartbeatProbe>(new AliveSandboxHeartbeat());
        });
}
