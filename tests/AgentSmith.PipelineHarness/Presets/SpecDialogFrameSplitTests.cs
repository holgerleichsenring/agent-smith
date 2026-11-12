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
/// outcome_format. A first turn's system prompt carries no format; a discussed turn gets the epic format from the
/// tool and its epic block resolves as an EpicOutcome.
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

    [Fact]
    public async Task SpecDialogHarness_FrameSplit_FormatArrivesOnlyThroughOutcomeFormat()
    {
        await using var harness = BuildHarness();
        harness.ChatClient.EnqueueText("Found the service; is a widget per tenant or global?");
        await TurnAsync(harness, State([new TranscriptTurn(TranscriptRole.User, "we need widgets", DateTimeOffset.UtcNow)]));
        var system = SystemPrompt(harness.ChatClient.CallMessages[0]);
        var firstCalls = harness.ChatClient.CallMessages.Count;
        harness.ChatClient.EnqueueToolCall("outcome_format", """{"kind": "epic"}""").EnqueueEpicWrite(Parent, Storage, Api)
            .EnqueueText("Two slices: the store has to exist before the endpoint reads it.");

        var result = await TurnAsync(harness, Discussed());

        system.Should().Contain("outcome_format(kind)", "the pinned master is what the prompt catalog served")
            .And.NotContain("```outcome").And.NotContain("kind: epic").And.NotContain("## Drafting a spec");
        ToolResults(harness.ChatClient.CallMessages[firstCalls + 1]).Should()
            .Contain(r => r.Contains("## Drafting a spec") && r.Contains("## Proposing an epic"));
        result.Outcome.Should().BeOfType<EpicOutcome>().Which.Children.Should().HaveCount(2);
    }

    private static string SystemPrompt(IReadOnlyList<ChatMessage> call) =>
        string.Join("\n", call.Where(m => m.Role == ChatRole.System).Select(m => m.Text));

    private static IEnumerable<string> ToolResults(IReadOnlyList<ChatMessage> call) =>
        call.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Select(r => r.Result?.ToString() ?? string.Empty);

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
