using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Demo;
using AgentSmith.Cli.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Domain.Entities;
using AgentSmith.Infrastructure.Core.Services.Demo;
using AgentSmith.Infrastructure.Core.Services.Skills;
using AgentSmith.PipelineHarness.Composition;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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


    [Fact]
    public async Task Demo_FixBug_InlineTicket_EndToEndWithScriptedChatClient()
    {
        var workspace = await MaterializeAsync();
        Directory.Exists(Path.Combine(workspace, ".git")).Should().BeTrue();

        await using var harness = Harness();
        // 2026-11-19-6b0ba: DeriveSpec writes the inline ticket as a stand-in spec without a model call.
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

    private static void ScriptTheFix(AgentSmith.PipelineHarness.Llm.ScriptedChatClient chat) => chat
        // p0394a: no plan slot — the spec is the plan, the master consumes next.
        .EnqueueToolCall("write_file", """{"path":"primary/src/Sample/PriceCalculator.cs","content":"// >= boundary fix"}""")
        .EnqueueToolCall("run_command", """{"command":"dotnet test tests/Sample.Tests/Sample.Tests.csproj","repo":"primary"}""")
        .EnqueueToolCall("update_progress", """{"items":[{"id":"boundary","activity":"Include the 100.00 boundary in the discount check","status":"done"}]}""")
        .EnqueueFinish(summary: "boundary fixed");

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
