using AgentSmith.Application.Prompts;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.PipelineHarness.Evals;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.SpecDialog;
using AgentSmith.PipelineHarness.Llm;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-05-862ec: the split design-partner frame through the REAL server composition over the EMBEDDED catalog,
/// as the scan eval reads it — the real prompt catalog renders the pinned master and the real reference source serves
/// the write tools. 2026-11-12-cd4cg: outcome_format is retired; a discussed turn writes its epic through write_spec.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SpecDialogFrameSplitTests
{
    private const string Repo = "spec-dialog-fixture";
    private const string Project = "fixture-spec-dialog";

    private const string Parent = "spec: p9000\ngoal: \"Widget platform end to end\"";
    private const string Storage =
        "spec: p9000a\ngoal: \"Widget storage layer\"\ndone:\n  - \"a widget is stored and read back\"";
    private const string Api =
        "spec: p9000b\ngoal: \"Widget API on top of the storage layer\"\nrequires: [p9000a]\ndone:\n  - \"the endpoint returns a stored widget\"";

    // 2026-11-12-cd4cg: no format is fetched — the write tools carry the schema in their own descriptions.
    [Fact]
    public async Task SpecDialogHarness_EpicWrittenThroughTheWriteTools_ResolvesAsAnEpic()
    {
        await using var harness = BuildHarness();
        harness.ChatClient.EnqueueEpicWrite(Parent, Storage, Api)
            .EnqueueText("Two slices: the store has to exist before the endpoint reads it.");

        var result = await TurnAsync(harness, Discussed());

        harness.ChatClient.FirstOptions!.Tools!.Select(t => t.Name).Should().NotContain("outcome_format")
            .And.Contain("write_spec");
        result.Outcome.Should().BeOfType<EpicOutcome>().Which.Children.Should().HaveCount(2);
    }

    private static async Task<SpecDialogTurnResult> TurnAsync(RealCompositionHarness harness, ConversationState state)
    {
        await using var scope = harness.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISpecDialogTurnRunner>()
            .RunTurnAsync(state, new TurnPoster("U1", false), CancellationToken.None);
    }

    private static RealCompositionHarness BuildHarness() =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            services.RemoveAll<ISkillsCatalogResolver>();
            services.AddSingleton<ISkillsCatalogResolver>(new StubCatalogResolver());
            services.RemoveAll<ISandboxHeartbeatProbe>();
            services.AddSingleton<ISandboxHeartbeatProbe>(new AliveSandboxHeartbeat());
            services.RemoveAll<ISkillsCatalogPath>();
            services.AddSingleton<ISkillsCatalogPath, EmbeddedSkillsCatalogPath>();
            services.RemoveAll<IPromptCatalog>();
            services.AddPromptCatalog();
        });

    private static ConversationState Discussed() => State(
    [
        new TranscriptTurn(TranscriptRole.User, "we need widgets", DateTimeOffset.UtcNow),
        new TranscriptTurn(TranscriptRole.Assistant, "Found the service; is a widget per tenant or global?", DateTimeOffset.UtcNow),
        new TranscriptTurn(TranscriptRole.User, "global; propose it", DateTimeOffset.UtcNow),
    ]);

    private static ConversationState State(IReadOnlyList<TranscriptTurn> transcript) => new()
    {
        JobId = "sess-frame-split",
        ChannelId = "C-harness",
        UserId = "U-harness",
        Platform = "dashboard",
        Project = Project,
        TicketId = string.Empty,
        StartedAt = DateTimeOffset.UtcNow,
        Mode = ConversationMode.SpecDialog,
        ThreadId = "th-frame-split",
        Transcript = [.. transcript],
        Scope = new ActiveScope { Project = Project, Repos = [Repo] },
    };
}
