using AgentSmith.Contracts.Providers;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-10-13-8762m fast-tier end-to-end: what spec 1's master remembered and handed forward is in the
/// user prompt spec 2's master is actually sent — the index re-read at the spec-execution start and
/// rendered in code, the note read from this run.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class BuildNoteHarnessTests
{

    private const string TwoSpecs = """
        {"specs": [
           {"slug": "introduce-the-guard",
            "goal": "Introduce the empty-payload guard",
            "steps": [{"id": "guard", "action": "Answer an empty request body with 400"}],
            "done": ["An empty request body is answered with 400."],
            "carries": [1,2,3]},
           {"slug": "migrate-the-callers",
            "goal": "Move the existing callers onto the guard",
            "steps": [{"id": "callers", "action": "Route every caller through the guard"}],
            "done": ["No caller builds its own empty-payload check."],
            "carries": [4,5,6]}],
         "discarded": [],
         "ignored_instructions": [],
         "handback": {"case": "none", "reason": ""}}
        """;

    [Fact]
    public async Task Harness_TwoSpecSeries_SpecTwoMasterPromptCarriesMemoryRememberedInSpecOne()
    {
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), services =>
        {
            HarnessProjectAnalyzerStub.Register(services);
            services.RemoveAll<ITicketProviderFactory>();
            services.AddSingleton<ITicketProviderFactory>(new RecordingTicketProviderFactory(new RecordingTicketProvider()));
        });
        harness.StubSandboxFactory!.ReportsWorkingTree = true; // remember needs a change to land with
        harness.ChatClient
            .EnqueueText(TwoSpecs)
            .EnqueueText("Planning: introduce the guard.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Guard.cs","content":"// guard"}""")
            .EnqueueToolCall("remember",
                """{"type":"project","name":"guard-home","description":"the empty-payload guard lives in src/Guard.cs","body":"One guard for every caller."}""")
            .EnqueueToolCall("hand_forward", """{"renamed":["PayloadCheck -> EmptyPayloadGuard"],"persisted":["file src/Guard.cs"]}""")
            .EnqueueFinish()
            .EnqueueText("Planning: migrate the callers.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Callers.cs","content":"// callers"}""")
            .EnqueueFinish();

        var runner = new PipelineRunner(harness.Services) { DoneStatus = "done", FailedStatus = "failed" };
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue();
        ToolResults(harness).Should().Contain(r => r.Contains("Memory 'guard-home' recorded"),
            "remember answered that it wrote the memory, not that it refused");
        var specTwo = harness.ChatClient.PromptsSeen.Where(p => p.Contains("Move the existing callers onto the guard")
            && p.Contains("## Spec contract")).ToList();
        specTwo.Should().NotBeEmpty();
        specTwo[0].Should().Contain("## Experiential memory index").And.Contain("guard-home")
            .And.Contain("## Build notes of the specs that already ran")
            .And.Contain("- renamed: PayloadCheck -> EmptyPayloadGuard");
    }

    private static IEnumerable<string> ToolResults(RealCompositionHarness harness) =>
        harness.ChatClient.CallMessages.SelectMany(m => m).SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>().Select(r => r.Result?.ToString() ?? string.Empty);
}
