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
/// drives the REAL fix-bug preset through the composition with an inline
/// ticket instead of a tracker — the exact request shape `agent-smith demo`
/// builds. Proves the two demo seams end-to-end: FetchTicket's inline
/// materialization and the trackerless run reaching a green keystone.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class DemoFixBugTests : IAsyncLifetime
{
    private readonly string _workspace = Path.Combine(
        Path.GetTempPath(), $"agentsmith-harness-demo-{Guid.NewGuid():N}");

    [Fact]
    public async Task Demo_FixBug_InlineTicket_EndToEndWithScriptedChatClient()
    {
        var materializer = new DemoWorkspaceMaterializer(
            new EmbeddedDemoSample(),
            new CatalogTarballExtractor(NullLogger<CatalogTarballExtractor>.Instance),
            new LocalGitProcessInitializer(),
            NullLogger<DemoWorkspaceMaterializer>.Instance);
        var workspace = await materializer.MaterializeAsync(_workspace, CancellationToken.None);
        Directory.Exists(Path.Combine(workspace, ".git")).Should().BeTrue();

        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Default), HarnessProjectAnalyzerStub.Register);
        harness.ChatClient
            // p0328: NegotiateExpectation drafts before GeneratePlan and drains one
            // FIFO slot; headless demo runs auto-ratify the draft as 'unratified'.
            .EnqueueText("""
                {"observed": "Bulk discount is not applied at exactly 100.00.",
                 "expected": ["Order totals of exactly 100.00 receive the bulk discount.", "Totals below 100.00 stay undiscounted."],
                 "constraints": ["No behavior change for totals above 100.00."],
                 "open_question": null}
                """)
            // GeneratePlan drains one FIFO slot before the master (p0276).
            .EnqueueText("Planning: fix the boundary comparison in PriceCalculator.")
            .EnqueueToolCall("write_file", """{"path":"primary/src/Sample/PriceCalculator.cs","content":"// >= boundary fix"}""")
            .EnqueueToolCall("run_command", """{"command":"dotnet test tests/Sample.Tests/Sample.Tests.csproj","repo":"primary"}""")
            .EnqueueText("""Done. {"status":"green","build_ran":true,"build_passed":true,"tests_ran":true,"tests_passed":true,"summary":"boundary fixed","acceptance":[{"criterion":"criterion 1","status":"met","evidence":"handled in the change"},{"criterion":"criterion 2","status":"met","evidence":"existing behaviour preserved"}]}""");

        var runner = new PipelineRunner(harness.Services)
        {
            SourcePathOverride = workspace,
            InlineTicket = new InlineTicket(
                "Bulk discount is not applied to orders of exactly 100.00",
                "Fix the boundary condition so the failing test passes.",
                "dotnet test tests/Sample.Tests/Sample.Tests.csproj"),
        };
        var result = await runner.RunAsync("fix-bug");

        result.IsSuccess.Should().BeTrue(
            $"the demo's inline-ticket fix-bug run must reach a green keystone: {result.Message}");
        var ticket = runner.LastContext!.Get<Ticket>(ContextKeys.Ticket);
        ticket.Source.Should().Be(InlineTicket.Source, "the run's requirement record is the inline payload");
        ticket.Title.Should().Contain("Bulk discount");
        harness.ChatClient.ToolCalls.ShouldHaveCalledInOrder("write_file", "run_command");
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        try { if (Directory.Exists(_workspace)) Directory.Delete(_workspace, recursive: true); }
        catch { /* best-effort cleanup */ }
        return Task.CompletedTask;
    }
}
