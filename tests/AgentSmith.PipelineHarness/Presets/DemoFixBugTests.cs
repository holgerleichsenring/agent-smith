using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Demo;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;
using AgentSmith.Infrastructure.Core.Services.Demo;
using AgentSmith.Infrastructure.Core.Services.Skills;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// p0326: the demo path, LLM-free. Materializes the REAL embedded sample
/// project (seeded bug + failing boundary test) into a temp workspace, then
/// drives the REAL coding preset through the composition with an inline
/// ticket instead of a tracker — the exact request shape `agent-smith demo`
/// builds. Proves the two demo seams end-to-end: FetchTicket's inline
/// materialization and the trackerless run reaching a green keystone.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class DemoFixBugTests : IAsyncLifetime
{
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(), $"agentsmith-harness-demo-{Guid.NewGuid():N}");

    private const string DerivedSpec = """
        {"specs": [
           {"slug": "bulk-discount-boundary",
            "goal": "Apply the bulk discount at exactly 100.00",
            "steps": [{"id": "boundary", "action": "Include the 100.00 boundary in the discount check"}],
            "done": ["Order totals of exactly 100.00 receive the bulk discount.",
                     "Totals below 100.00 stay undiscounted."],
            "carries": [1,2,3,4,5,6,7,8]}],
         "discarded": [], "ignored_instructions": [],
         "handback": {"case": "none", "reason": ""}}
        """;

    // 2026-11-02-2b26d: a REFUSE is admitted only on a look of the reviewer's own, so each of the panel's two
    // reviewers searches first; whichever answers after its search refuses on that search.
    private const string Refuse = """
        {"verdict": "REFUSE", "cuts": [], "findings": [{"text": "Nothing in the sample names a bulk discount rule.",
          "repository": "default", "path": null, "line": 0, "cites": "A1"}]}
        """;

    [Fact]
    public async Task Demo_FixBug_InlineTicket_EndToEndWithScriptedChatClient()
    {
        var workspace = await MaterializeAsync();
        Directory.Exists(Path.Combine(workspace, ".git")).Should().BeTrue();

        await using var harness = Harness();
        // p0393a: DeriveSpec turns the ticket into the spec set and drains one FIFO slot. The spec's done-list
        // is the run's acceptance contract — p0328's negotiation left the code pipeline with it.
        harness.ChatClient.EnqueueText(DerivedSpec);
        ScriptTheFix(harness.ChatClient);

        var runner = Runner(harness, workspace);
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue(
            $"the demo's inline-ticket bug-fix run must reach a green keystone: {result.Message}");
        var ticket = runner.LastContext!.Get<Ticket>(ContextKeys.Ticket);
        ticket.Source.Should().Be(InlineTicket.Source, "the run's requirement record is the inline payload");
        ticket.Title.Should().Contain("Bulk discount");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file", "run_command", "update_progress");
    }

    /// <summary>2026-11-02-2b26d: a review objection standing on the demo's inline run is asked in the terminal;
    /// nobody answers, the default reading builds the reviewed spec, and the fix is still made.</summary>
    [Fact]
    public async Task Demo_ReviewObjection_DefaultReading_StillCommitsTheFix()
    {
        var workspace = await MaterializeAsync();
        await using var harness = Harness();
        harness.StubSandboxFactory!.AdvancesHeadOnCommit = true; // the review's revision is a second commit
        harness.ChatClient
            .EnqueueSpecAttackToolCall("search_repository", """{"repository": "default", "pattern": "BulkDiscountRule"}""")
            .EnqueueSpecAttackToolCall("search_repository", """{"repository": "default", "pattern": "BulkDiscountRule"}""")
            .EnqueueSpecAttackReply(Refuse)
            .EnqueueSpecAttackReply(Refuse)
            .EnqueueText(DerivedSpec)
            // The rewrite after a REFUSE without cuts reads the main FIFO; a reply that is no spec leaves it standing.
            .EnqueueText("The spec cannot be rewritten from what was opened.");
        ScriptTheFix(harness.ChatClient);

        var runner = Runner(harness, workspace);
        var result = await runner.RunAsync("code");

        result.IsSuccess.Should().BeTrue($"the default reading builds the reviewed spec: {result.Message}");
        var set = runner.LastContext!.Get<AgentSmith.Contracts.Specs.SpecSet>(ContextKeys.SpecSet);
        set.Handback!.QuestionKind.Should().Be(AgentSmith.Contracts.Specs.SpecQuestionKind.ReviewObjection,
            "the objection was asked");
        runner.LastContext!.Has(ContextKeys.SpecHandback).Should().BeFalse("the default reading was taken");
        harness.ChatClient.ToolCalls.Select(c => c.Name).Should().Contain("write_file");
    }

    private static void ScriptTheFix(AgentSmith.PipelineHarness.Llm.ScriptedChatClient chat) => chat
        // p0394a: no plan slot — the spec is the plan, the master consumes next.
        .EnqueueToolCall("write_file", """{"path":"primary/src/Sample/PriceCalculator.cs","content":"// >= boundary fix"}""")
        .EnqueueToolCall("run_command", """{"command":"dotnet test tests/Sample.Tests/Sample.Tests.csproj","repo":"primary"}""")
        .EnqueueToolCall("update_progress", """{"items":[{"id":"boundary","activity":"Include the 100.00 boundary in the discount check","status":"done"}]}""")
        .EnqueueText("""Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"boundary fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled in the change"},{"criterion":"criterion 2","status":"met","evidence":"existing behaviour preserved"}]}""");

    private Task<string> MaterializeAsync() => new DemoWorkspaceMaterializer(
            new EmbeddedDemoSample(),
            new CatalogTarballExtractor(NullLogger<CatalogTarballExtractor>.Instance),
            new LocalGitProcessInitializer(),
            NullLogger<DemoWorkspaceMaterializer>.Instance)
        .MaterializeAsync(_workspace, CancellationToken.None);

    private static RealCompositionHarness Harness() =>
        RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);

    private static PipelineRunner Runner(RealCompositionHarness harness, string workspace) => new(harness.Services)
    {
        SourcePathOverride = workspace,
        InlineTicket = new InlineTicket(
            "Bulk discount is not applied to orders of exactly 100.00",
            "Fix the boundary condition so the failing test passes.",
            "dotnet test tests/Sample.Tests/Sample.Tests.csproj"),
    };

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        try { if (Directory.Exists(_workspace)) Directory.Delete(_workspace, recursive: true); }
        catch { /* best-effort cleanup */ }
        return Task.CompletedTask;
    }
}
